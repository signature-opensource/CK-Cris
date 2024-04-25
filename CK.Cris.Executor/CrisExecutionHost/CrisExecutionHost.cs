using CK.Core;
using CK.PerfectEvent;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CK.Core.CheckedWriteStream;
using static Microsoft.IO.RecyclableMemoryStreamManager;

namespace CK.Cris
{
    /// <summary>
    /// A Cris execution host handles <see cref="CrisJob"/> (submitted by <see cref="ContainerCommandExecutor"/>)
    /// in the background thanks to a variable count of parallel runners.
    /// <para>
    /// This is a <see cref="ISingletonAutoService"/>: the default instance is available in all the DI containers
    /// (but nothing prevents other host to be instantiated and used independently).
    /// </para>
    /// </summary>
    [Setup.AlsoRegisterType( typeof( ICrisJobResult ) )]
    [Setup.AlsoRegisterType( typeof( RawCrisValidator ) )]
    [Setup.AlsoRegisterType( typeof( CrisExecutionContext ) )]
    public sealed partial class CrisExecutionHost : ICrisExecutionHost, ISingletonAutoService
    {
        readonly IPocoFactory<ICrisJobResult> _jobResultFactory;
        readonly IPocoFactory<ICrisResultError> _errorResultFactory;
        readonly DarkSideCrisEventHub _eventHub;
        readonly RawCrisValidator _commandValidator;
        internal readonly RawCrisExecutor _rawExecutor;

        readonly PerfectEventSender<ICrisExecutionHost> _parallelRunnerCountChanged;
        // We use null as the close signal for runners and push int values to regulate the count of
        // runners.
        // The _channel is used as the lock to manage runners.
        // This is a multi-writers/multi-readers channel: background execution
        // can be requested concurrently and multiple parallel runners run.
        readonly Channel<object?> _channel;
        Runner? _last;
        int _runnerCount;
        int _plannedRunnerCount;
        // Ever increasing number.
        int _runnerNumber;

        /// <summary>
        /// Initializes a new <see cref="CrisExecutionHost"/> with a single initial runner.
        /// </summary>
        /// <param name="eventHub">The cris event hub.</param>
        /// <param name="validator">The command validator.</param>
        /// <param name="executor">The command executor.</param>
        public CrisExecutionHost( DarkSideCrisEventHub eventHub, RawCrisValidator validator, RawCrisExecutor executor )
        {
            Throw.CheckNotNullArgument( eventHub );
            Throw.CheckNotNullArgument( validator );
            Throw.CheckNotNullArgument( executor );
            _jobResultFactory = eventHub.PocoDirectory.Find<ICrisJobResult>()!;
            _errorResultFactory = eventHub.PocoDirectory.Find<ICrisResultError>()!;
            _eventHub = eventHub;
            _commandValidator = validator;
            _rawExecutor = executor;
            _channel = Channel.CreateUnbounded<object?>();
            _parallelRunnerCountChanged = new PerfectEventSender<ICrisExecutionHost>();
            _plannedRunnerCount = 1;
            _runnerCount = 1;
            _last = new Runner( this, 0, null );
        }

        /// <inheritdoc />
        public int ParallelRunnerCount
        {
            get => _runnerCount;
            set
            {
                Throw.CheckOutOfRangeArgument( value >= 1 && value <= 1000 );
                Push( value );
            }
        }

        /// <inheritdoc />
        public PerfectEvent<ICrisExecutionHost> ParallelRunnerCountChanged => _parallelRunnerCountChanged.PerfectEvent;

        /// <summary>
        /// Starts a <see cref="CrisJob"/> execution: the job is sent to one available runner
        /// and will be executed in the background.
        /// </summary>
        /// <param name="job">The job to execute.</param>
        public void StartJob( CrisJob job )
        {
            Throw.CheckNotNullArgument( job );
            Push( job );
        }

        void Push( object job ) => _channel.Writer.TryWrite( job );

        ValueTask ExecuteTypedJobAsync( IActivityMonitor monitor, object o )
        {
            if( o is CrisJob job ) return HandleCommandAsync( monitor, job );
            return HandleSetRunnerCountAsync( monitor, (int)o );
        }

        async ValueTask HandleCommandAsync( IActivityMonitor monitor, CrisJob job )
        {
            using var gLog = monitor.StartDependentActivityGroup( job.IssuerToken );

            AsyncServiceScope scoped = default;
            bool isScopedCreated = false;

            // Should the path with validation be code generated for the services to be resolved only
            // once across the Validation and Execution methods?

            // This is null until validation is done.
            ICrisJobResult? crisResult = null;
            try
            {
                scoped = job._executor.CreateAsyncScope( job );
                isScopedCreated = true;
                // Configure the data for the DI endpoint: the monitor
                // is the one of the calling runner and the ExecutionContext
                // is bound to the new scoped service.
                job._runnerMonitor = monitor;
                var rootContext = new CrisJob.JobExecutionContext( job, monitor, scoped.ServiceProvider, _eventHub, _rawExecutor );
                // This ExecutionContext is now available in the DI container. Work can start.
                job._executionContext = rootContext;

                CrisValidationResult validation;
                if( job._skipValidation )
                {
                    validation = CrisValidationResult.SuccessResult;
                }
                else
                {
                    validation = await _commandValidator.ValidateCommandAsync( monitor, scoped.ServiceProvider, job.Command, gLog );
                }
                // Sets the validation result on the executing command if there is one.
                if( job._executingCommand != null )
                {
                    if( !validation.Success )
                    {
                        var error = _errorResultFactory.Create();
                        error.Errors.AddRange( validation.Messages );
                        error.LogKey = validation.LogKey;
                        error.IsValidationError = true;
                        job._executingCommand.DarkSide.SetValidationResult( validation, error );
                    }
                    else
                    {
                        job._executingCommand.DarkSide.SetValidationResult( validation, null );
                    }
                }
                // Always call OnCrisValidationResultAsync even if it is successful: this is
                // the "execution started" signal for the executor.
                await job._executor.OnCrisValidationResultAsync( monitor, job, validation );
                // If validation fails, we are done.
                if( !validation.Success ) return;

                crisResult = _jobResultFactory.Create();
                // Executing the command (handlers and post handlers).
                var (result,finalEvents) = await rootContext.ExecuteAsync( job.Command );
                // Sets the result on the executing command if there is one.
                job._executingCommand?.DarkSide.SetResult( finalEvents, result );
                // Send the result. We are done.
                crisResult.Result = result;
                await job._executor.SetFinalResultAsync( monitor, job, finalEvents, crisResult );
            }
            catch( Exception ex )
            {
                // Sets the exception on the executing command Completion if there is one.
                job._executingCommand?.DarkSide.SetException( ex );
                // Ensures that the crisResult exists and sets its Result to a ICrisErrorResult.
                var currentCulture = isScopedCreated ? scoped.ServiceProvider.GetService<CurrentCultureInfo>() : null;
                crisResult ??= _jobResultFactory.Create();
                ICrisResultError error = _errorResultFactory.Create();
                crisResult.Result = error;
                PocoFactoryExtensions.OnUnhandledError( monitor, ex, job.Command, true, currentCulture, error.Errors.Add );
                error.LogKey = gLog.GetLogKeyString();

                // Sets the SafeCompletion with the ICrisResultError on the executing command if there is one.
                var noEvents = Array.Empty<IEvent>();
                job._executingCommand?.DarkSide.SetResult( noEvents, error );
                // Send the error. We are done.
                await job._executor.SetFinalResultAsync( monitor, job, noEvents, crisResult );
            }
            finally
            {
                if( isScopedCreated ) await scoped.DisposeAsync();
            }
        }

    }

}
