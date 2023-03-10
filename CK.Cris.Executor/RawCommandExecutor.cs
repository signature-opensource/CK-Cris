using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Executes commands on services provided to <see cref="ExecuteCommandAsync(IActivityMonitor, IServiceProvider, ICommand)"/>.
    /// This class is agnostic of the context since the <see cref="IServiceProvider"/> defines the execution context: this is a true
    /// singleton, the same instance can be used to execute any locally handled commands.
    /// <para>
    /// The concrete class implements all the generated code that routes the command to its handler.
    /// </para>
    /// </summary>
    [CK.Setup.ContextBoundDelegation( "CK.Setup.Cris.RawCommandExecutorImpl, CK.Cris.Executor.Engine" )]
    public abstract class RawCommandExecutor : ISingletonAutoService
    {
        readonly IPocoFactory<ICrisResult> _resultFactory;
        readonly IPocoFactory<ICrisResultError> _simpleErrorResultFactory;

        /// <summary>
        /// Initializes a new <see cref="RawCommandExecutor"/>.
        /// </summary>
        /// <param name="resultFactory">The command result factory.</param>
        /// <param name="simpleErrorResultFactory">The simple error result factory.</param>
        public RawCommandExecutor( IPocoFactory<ICrisResult> resultFactory, IPocoFactory<ICrisResultError> simpleErrorResultFactory )
        {
            _resultFactory = resultFactory;
            _simpleErrorResultFactory = simpleErrorResultFactory;
        }

        /// <summary>
        /// Executes a command by calling the ExecuteCommand or ExecuteCommandAsync method for the
        /// closure of the command Poco (the ICommand interface that unifies all other ICommand and <see cref="ICommandPart"/>).
        /// Any exceptions are caught and sent to the <see cref="IFrontCommandExceptionHandler"/> service.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>The <see cref="ICrisResult"/>.</returns>
        public async Task<ICrisResult> ExecuteCommandAsync( IActivityMonitor monitor, IServiceProvider services, ICommand command )
        {
            try
            {
                var o = await DoExecuteCommandAsync( monitor, services, command );
                return _resultFactory.Create( r => { r.Code = VESACode.Synchronous; r.Result = o; } );
            }
            catch( Exception ex )
            {
                var r = _resultFactory.Create();
                r.Code = VESACode.Error;
                try
                {
                    var errorHandler = services.GetService<IFrontCommandExceptionHandler>();
                    if( errorHandler == null )
                    {
                        monitor.Error( $"Error while executing {command.GetType():C} occurred (no IFrontCommandExceptionHandler service available in the Services).", ex );
                        r.Result = _simpleErrorResultFactory.Create( ex.Message );
                    }
                    else
                    {
                        await errorHandler.OnErrorAsync( monitor, services, ex, command, r );
                        if( r.Result == null || (r.Result is IEnumerable e && !e.GetEnumerator().MoveNext()) )
                        {
                            var msg = $"IFrontCommandExceptionHandler '{errorHandler.GetType().Name}' failed to add any error result. The exception message is added.";
                            monitor.Error( msg );
                            r.Result = _simpleErrorResultFactory.Create( msg, ex.Message );
                        }
                    }
                }
                catch( Exception ex2 )
                {
                    using( monitor.OpenFatal( "Error in ErrorHandler.", ex2 ) )
                    {
                        monitor.Error( "Original error.", ex );
                    }
                    r.Result = ex2.Message;
                }
                return r;
            }
        }

        protected abstract Task<object> DoExecuteCommandAsync( IActivityMonitor monitor, IServiceProvider services, ICommand command );
    }
}
