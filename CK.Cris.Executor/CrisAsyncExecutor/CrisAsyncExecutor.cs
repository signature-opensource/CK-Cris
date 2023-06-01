using CK.Core;
using CK.PerfectEvent;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.Cris
{

    [EndpointDefinition]
    public abstract class CrisEndpointDefinition : EndpointDefinition<CrisAsyncJob>
    {
        public override void ConfigureEndpointServices( IServiceCollection services,
                                                        Func<IServiceProvider,CrisAsyncJob> scopeData,
                                                        IServiceProviderIsService globalServiceExists )
        {
            services.AddScoped( sp => scopeData( sp )._runnerMonitor! );
        }
    }

    /// <summary>
    /// Non generic base class for <see cref="CrisExecutor{T}"/> that
    /// implements <see cref="ICrisAsyncExecutor"/> interface.
    /// </summary>
    [CKTypeSuperDefiner]
    [Setup.AlsoRegisterType( typeof( ICrisJobResult ) )]
    public abstract partial class CrisAsyncExecutor : ICrisAsyncExecutor
    {
        readonly IPocoFactory<ICrisJobResult> _resultFactory;
        readonly IPocoFactory<ICrisResultError> _errorResultFactory;
        readonly IEndpointType<CrisAsyncJob> _endpoint;

        readonly PerfectEventSender<ICrisAsyncExecutor> _parallelRunnerCountChanged;
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

        private protected CrisAsyncExecutor( IServiceProvider serviceProvider )
        {
            _resultFactory = serviceProvider.GetRequiredService<IPocoFactory<ICrisJobResult>>();
            _errorResultFactory = serviceProvider.GetRequiredService<IPocoFactory<ICrisResultError>>();
            _endpoint = serviceProvider.GetRequiredService<IEndpointType<CrisAsyncJob>>();

            _channel = Channel.CreateUnbounded<object?>();
            _parallelRunnerCountChanged = new PerfectEventSender<ICrisAsyncExecutor>();
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
        public PerfectEvent<ICrisAsyncExecutor> ParallelRunnerCountChanged => _parallelRunnerCountChanged.PerfectEvent;

        private protected void Push( object job ) => _channel.Writer.TryWrite( job );

        private protected virtual ValueTask ExecuteTypedJobAsync( IActivityMonitor monitor, object job )
        {
            if( job is int count )
            {
                return HandleSetRunnerCountAsync( monitor, count );
            }
            monitor.Error( $"Unhandled job type '{job.GetType()}'." );
            return default;
        }

        async ValueTask HandleCommandAsync( IActivityMonitor monitor, CrisAsyncJob c )
        {
            using var log = monitor.StartDependentActivity( c.IssuerToken );

            // Creates the scoped services.
            // Sets the runner monitor on the job: it will be the monitor used
            // by validation and execution.
            c._runnerMonitor = monitor;

            AsyncServiceScope scoped;

            // TODO: This should be code generated for 2 reasons:
            // - The warnings of the ConfigureServices will appear in the validation result (currently they are lost).
            // - The services would be resolved only once across the Validation and Execution methods.
            var step = "configuring services for";
            try
            {
                scoped = _endpoint.GetContainer().CreateAsyncScope( c );
                using( monitor.CollectEntries( out var entries, LogLevelFilter.Warn, 200 ) )
                {
                    c.Endpoint.ConfigureServices( monitor, c.Request, services );
                    var v = CrisValidationResult.Create( entries );
                    if( !v.Success )
                    {
                        // At least one error occurred while configuring the services.
                        // Send the faulted validation result and we are done.
                        c.Endpoint.ReturnCrisValidationResult( monitor, c.Request, v );
                        return;
                    }
                }
                step = "validating";
                var validation = await _commandValidator.ValidateCrisPocoAsync( monitor, services, c.Request.Payload );
                // Always send the CrisValidationResult even if it is successful: this is the "execution started" signal.
                c.Endpoint.ReturnCrisValidationResult( monitor, c.Request, validation );
                // If validation fails, we are done.
                if( !validation.Success ) return;
                // Executing the command (handlers and post handlers).
                step = "executing";
                var result = await _commandExecutor.RawExecuteAsync( services, c.Request.Payload );
                // Send the result. We are done.
                var r = _resultFactory.Create();
                r.Result = result;
                c.Endpoint.ReturnCommandResult( monitor, c.Request, r );
            }
            catch( Exception ex )
            {
                using( monitor.OpenError( $"While {step} command '{c.Request.Payload.CrisPocoModel.PocoName}'." ) )
                {
                    monitor.Error( c.Request.Payload.ToString()!, ex );
                }
                // Send the error. We are done.
                var error = _errorResultFactory.Create( e => e.Errors.Add( ex.Message ) );
                // This duplicates the code above but if an exception occurs here (it will be caught and
                // log by the Runner), we want to dispose the services.
                var r = _resultFactory.Create();
                r.Result = error;
                c.Endpoint.ReturnCommandResult( monitor, c.Request, r );
            }
            finally
            {
                await scoped.DisposeAsync();
            }
        }

    }


}
