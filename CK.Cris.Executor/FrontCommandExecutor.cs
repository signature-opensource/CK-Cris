using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Executes commands on all the available services: there is no restriction on the
    /// kind of the executing services since they are called in the "front" context. 
    /// </summary>
    [CK.Setup.ContextBoundDelegation( "CK.Setup.Cris.FrontCommandExecutorImpl, CK.Cris.Executor.Engine" )]
    public abstract class FrontCommandExecutor : ISingletonAutoService
    {
        protected readonly CommandDirectory Directory;
        protected readonly IPocoFactory<ICrisResult> ResultFactory;
        protected readonly IFrontCommandExceptionHandler ErrorHandler;
        readonly IPocoFactory<ICrisErrorResult> _simpleErrorResultFactory;

        /// <summary>
        /// Initializes a new <see cref="FrontCommandExecutor"/>.
        /// </summary>
        /// <param name="directory">The command directory.</param>
        /// <param name="resultFactory">The command result factory.</param>
        /// <param name="errorHandler">The error handler.</param>
        /// <param name="simpleErrorResultFactory">The simple error result factory.</param>
        public FrontCommandExecutor( CommandDirectory directory, IPocoFactory<ICrisResult> resultFactory, IFrontCommandExceptionHandler errorHandler, IPocoFactory<ICrisErrorResult> simpleErrorResultFactory )
        {
            Directory = directory;
            ResultFactory = resultFactory;
            ErrorHandler = errorHandler;
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
                return ResultFactory.Create( r => { r.Code = VESACode.Synchronous; r.Result = o; } );
            }
            catch( Exception ex )
            {
                var r = ResultFactory.Create();
                r.Code = VESACode.Error;
                try
                {
                    await ErrorHandler.OnErrorAsync( monitor, services, ex, command, r );
                    if( r.Result == null || (r.Result is IEnumerable e && !e.GetEnumerator().MoveNext()) )
                    {
                        var msg = $"IFrontCommandExceptionHandler '{ErrorHandler.GetType().Name}' failed to add any error result. The exception message is added.";
                        monitor.Error( msg );
                        r.Result = _simpleErrorResultFactory.Create( msg, ex.Message );
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
