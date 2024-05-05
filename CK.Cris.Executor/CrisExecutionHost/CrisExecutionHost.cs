using CK.Core;
using CK.PerfectEvent;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Immutable;
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
    [Setup.AlsoRegisterType( typeof( RawCrisReceiver ) )]
    [Setup.AlsoRegisterType( typeof( CrisExecutionContext ) )]
    public sealed partial class CrisExecutionHost : ICrisExecutionHost, ISingletonAutoService
    {
        readonly IPocoFactory<ICrisResultError> _errorResultFactory;
        readonly DarkSideCrisEventHub _eventHub;
        readonly RawCrisReceiver _commandValidator;
        private readonly RawCrisExecutor _rawExecutor;

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
        public CrisExecutionHost( DarkSideCrisEventHub eventHub, RawCrisReceiver validator, RawCrisExecutor executor )
        {
            Throw.CheckNotNullArgument( eventHub );
            Throw.CheckNotNullArgument( validator );
            Throw.CheckNotNullArgument( executor );
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

            ImmutableArray<UserMessage> validationMessages = ImmutableArray<UserMessage>.Empty;
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

                if( job._incomingValidationCheck )
                {
                    var aaa = scoped.ServiceProvider.GetService<AmbientServiceHub>();



                    var validation = await _commandValidator.IncomingValidateAsync( monitor, scoped.ServiceProvider, job.Command, gLog );
                    if( !validation.Success )
                    {
                        var error = _errorResultFactory.Create();
                        error.Errors.AddRange( validation.ErrorMessages );
                        error.LogKey = validation.LogKey;
                        error.IsValidationError = true;
                        job._executingCommand?.DarkSide.SetResult( error, validation.ValidationMessages, ImmutableArray<IEvent>.Empty );
                        await job._executor.SetFinalResultAsync( monitor, job, error, validation.ValidationMessages, ImmutableArray<IEvent>.Empty );
                        return;
                    }
                    validationMessages = validation.ValidationMessages;
                }
                // Executing the command (handlers and post handlers).
                var executed = await rootContext.ExecuteRootCommandAsync( job.Command );
                // Sets the result on the executing command if there is an executing command.
                validationMessages = validationMessages.IsEmpty
                                        ? executed.ValidationMessages
                                        : validationMessages.AddRange( executed.ValidationMessages );
                job._executingCommand?.DarkSide.SetResult( executed.Result, validationMessages, executed.Events );
                await job._executor.SetFinalResultAsync( monitor, job, executed.Result, validationMessages, executed.Events );
            }
            catch( Exception ex )
            {
                // Ensures that the crisResult exists and sets its Result to a ICrisErrorResult.
                var currentCulture = isScopedCreated ? scoped.ServiceProvider.GetService<CurrentCultureInfo>() : null;
                ICrisResultError error = _errorResultFactory.Create();
                PocoFactoryExtensions.OnUnhandledError( monitor, ex, job.Command, true, currentCulture, error.Errors.Add );
                error.LogKey = gLog.GetLogKeyString();

                job._executingCommand?.DarkSide.SetResult( error, validationMessages, ImmutableArray<IEvent>.Empty );
                await job._executor.SetFinalResultAsync( monitor, job, error, validationMessages, ImmutableArray<IEvent>.Empty );
            }
            finally
            {
                if( isScopedCreated ) await scoped.DisposeAsync();
            }
        }

    }

}
