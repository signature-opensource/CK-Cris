using CK.Core;
using CK.PerfectEvent;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Non generic base class for <see cref="CrisExecutor{T}"/> that
    /// implements <see cref="ICrisExecutionHost"/> interface.
    /// </summary>
    [CKTypeSuperDefiner]
    [Setup.AlsoRegisterType( typeof( ICrisJobResult ) )]
    public sealed partial class CrisExecutionHost : ICrisExecutionHost
    {
        readonly IPocoFactory<ICrisJobResult> _resultFactory;
        readonly IPocoFactory<ICrisResultError> _errorResultFactory;
        readonly PocoDirectory _pocoDirectory;
        readonly RawCrisValidator _commandValidator;
        readonly RawCrisExecutor _commandExecutor;

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

        public CrisExecutionHost( PocoDirectory pocoDirectory, RawCrisValidator validator, RawCrisExecutor executor )
        {
            _pocoDirectory = pocoDirectory;
            _resultFactory = pocoDirectory.Find<ICrisJobResult>()!;
            _errorResultFactory = pocoDirectory.Find<ICrisResultError>()!;
            _commandValidator = validator;
            _commandExecutor = executor;
            _channel = Channel.CreateUnbounded<object?>();
            _parallelRunnerCountChanged = new PerfectEventSender<ICrisExecutionHost>();
            _runnerCount = 1;
            _plannedRunnerCount = 1;
            _last = new Runner( this, 0, null );
        }

        /// <inheritdoc />
        public abstract IEndpointType EndpointType { get; }

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

        sealed record class ExecuteJob( TheTruc Truc, CrisJob Job );

        public void BackgroundExecute( TheTruc truc, IAbstractCommand command, ActivityMonitor.Token issuerToken )
        {
            Throw.CheckNotNullArgument( truc );
            Throw.CheckNotNullArgument( command );
            Throw.CheckNotNullArgument( issuerToken );
            Push( new ExecuteJob( truc, truc.CreateJob( command, issuerToken ) ) );
        }

        private protected void Push( object job ) => _channel.Writer.TryWrite( job );

        private protected virtual ValueTask ExecuteTypedJobAsync( IActivityMonitor monitor, object job )
        {
            if( job is ExecuteJob eJob ) return HandleCommandAsync( monitor, eJob );
            return HandleSetRunnerCountAsync( monitor, (int)job );
        }

        async ValueTask HandleCommandAsync( IActivityMonitor monitor, ExecuteJob eJob )
        {
            using var log = monitor.StartDependentActivity( eJob.Job.IssuerToken );

            // Creates the scoped services.
            // Sets the runner monitor on the job: it will be the monitor used by validation and execution.
            eJob.Job._runnerMonitor = monitor;
            // Initializes the root execution context.
            eJob.Job._executionContext = new ExecutionContext( monitor, this, eJob.Truc, eJob.Job.Command );

            AsyncServiceScope scoped = default;
            bool isScopedCreated = false;

            // TODO: This should be code generated for the services to be resolved only once across the Validation and Execution methods.
            var step = "validating";
            try
            {
                scoped = eJob.Truc.CreateAsyncScope( eJob.Job );
                isScopedCreated = true;
                var validation = await _commandValidator.ValidateCommandAsync( monitor, scoped.ServiceProvider, eJob.Job.Command );
                // Always send the CrisValidationResult even if it is successful: this is the "execution started" signal.
                await eJob.Truc.ReturnCrisValidationResultAsync( monitor, eJob.Job, validation );
                // If validation fails, we are done.
                if( !validation.Success ) return;
                // Executing the command (handlers and post handlers).
                step = "executing";
                var result = await _commandExecutor.RawExecuteAsync( scoped.ServiceProvider, eJob.Job.Command );
                // Send the result. We are done.
                var r = _resultFactory.Create();
                r.Result = result;
                await eJob.Truc.ReturnCommandResultAsync( monitor, eJob.Job, r );
            }
            catch( Exception ex )
            {
                using( monitor.OpenError( $"While {step} command '{eJob.Job.Command.CrisPocoModel.PocoName}'." ) )
                {
                    monitor.Error( eJob.Job.Command.ToString()!, ex );
                }
                // Send the error. We are done.
                var error = _errorResultFactory.Create( e => e.Errors.Add( ex.Message ) );
                // This duplicates the code above but if an exception occurs here (it will be caught and
                // log by the Runner), we want to dispose the services.
                var r = _resultFactory.Create();
                r.Result = error;
                await eJob.Truc.ReturnCommandResultAsync( monitor, eJob.Job, r );
            }
            finally
            {
                if( isScopedCreated ) await scoped.DisposeAsync();
            }
        }

    }

}
