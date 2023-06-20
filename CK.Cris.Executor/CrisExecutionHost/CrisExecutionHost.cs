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
    /// A Cris execution host handles <see cref="CrisJob"/> (submitted by <see cref="AbstractCommandExecutor"/>)
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
        readonly IPocoFactory<ICrisJobResult> _resultFactory;
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
        /// The executor returns this result wrapper for 2 reasons:
        /// <list type="bullet">
        /// <item>
        /// This enables serialization to always consider any Poco serialization implementation
        /// and guaranties that only Poco compliant types are returned to the caller.
        /// </item>
        /// <item>
        /// This acts as a union type: the result is a <see cref="ICrisResultError"/>, or any other type
        /// or null. A <see cref="ICommand{TResult}"/> where TResult is a ICrisResultError can return
        /// an error (or null), and if TResult is <c>object?</c>, the handler can return any object,
        /// an error, or null.
        /// </item>
        /// </list>
        /// Defining a nested Poco (and registering it thanks to the <see cref="Setup.AlsoRegisterTypeAttribute"/>) makes
        /// it non extensible (and this is a good thing).
        /// </summary>
        [ExternalName( "CrisJobResult" )]
        public interface ICrisJobResult : IPoco
        {
            /// <summary>
            /// Gets or sets the execution result.
            /// </summary>
            object? Result { get; set; }
        }

        /// <summary>
        /// Initializes a new <see cref="CrisExecutionHost"/> with a single initial runner.
        /// </summary>
        /// <param name="pocoDirectory">The Poco directory.</param>
        /// <param name="validator">The command validator.</param>
        /// <param name="executor">The command executor.</param>
        public CrisExecutionHost( DarkSideCrisEventHub eventHub, RawCrisValidator validator, RawCrisExecutor executor )
        {
            Throw.CheckNotNullArgument( eventHub );
            Throw.CheckNotNullArgument( validator );
            Throw.CheckNotNullArgument( executor );
            _resultFactory = eventHub.PocoDirectory.Find<ICrisJobResult>()!;
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

        /// <summary>
        /// Gets or sets the number of parallel runners that handle the requests.
        /// It must be between 1 and 1000.
        /// </summary>
        public int ParallelRunnerCount
        {
            get => _runnerCount;
            set
            {
                Throw.CheckOutOfRangeArgument( value >= 1 && value <= 1000 );
                Push( value );
            }
        }

        /// <summary>
        /// Raised whenever the <see cref="ParallelRunnerCount"/> changes.
        /// </summary>
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
            using var log = monitor.StartDependentActivity( job.IssuerToken );

            AsyncServiceScope scoped = default;
            bool isScopedCreated = false;

            // Should the path with validation be code generated for the services to be resolved only
            // once across the Validation and Execution methods?
            var crisResult = _resultFactory.Create();
            var step = "validating";
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
                    validation = await _commandValidator.ValidateCommandAsync( monitor, scoped.ServiceProvider, job.Command );
                }
                job._executingCommand?.DarkSide.SetValidationResult( _errorResultFactory, validation );
                // Always call OnCrisValidationResultAsync even if it is successful: this is
                // the "execution started" signal for the executor.
                await job._executor.OnCrisValidationResultAsync( monitor, job, validation );
                // If validation fails, we are done.
                if( !validation.Success ) return;

                // Executing the command (handlers and post handlers).
                step = "executing";
                var (result,finalEvents) = await rootContext.ExecuteAsync( job.Command );
                job._executingCommand?.DarkSide.SetResult( finalEvents, result );
                // Send the result. We are done.
                crisResult.Result = result;
                await job._executor.SetFinalResultAsync( monitor, job, finalEvents, crisResult );
            }
            catch( Exception ex )
            {
                using( monitor.OpenError( $"While {step} command '{job.Command.CrisPocoModel.PocoName}'." ) )
                {
                    monitor.Error( job.Command.ToString()!, ex );
                }
                // Send the error. We are done.
                job._executingCommand?.DarkSide.SetException( ex );
                var error = _errorResultFactory.Create( e => e.Errors.Add( ex.Message ) );
                crisResult.Result = error;
                await job._executor.SetFinalResultAsync( monitor, job, Array.Empty<IEvent>(), crisResult );
            }
            finally
            {
                if( isScopedCreated ) await scoped.DisposeAsync();
            }
        }

    }

}
