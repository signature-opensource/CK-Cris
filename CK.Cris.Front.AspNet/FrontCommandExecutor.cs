using CK.AspNet;
using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Cris
{

    /// <summary>
    /// Executes commands on all the available services: there is no restriction on the
    /// kind of the executing services since they are called in the "front" context. 
    /// </summary>
    [CK.Setup.ContextBoundDelegation( "CK.Setup.Cris.FrontCommandExecutorImpl, CK.Cris.Front.AspNet.Runtime" )]
    public abstract class FrontCommandExecutor : ISingletonAutoService
    {
        protected CommandDirectory Directory;

        /// <summary>
        /// Initializes a new <see cref="FrontCommandExecutor"/>.
        /// </summary>
        /// <param name="directory">The command directory.</param>
        public FrontCommandExecutor( CommandDirectory directory )
        {
            Directory = directory;
        }

        /// <summary>
        /// Executes a command by calling the ExecuteCommand or ExecuteCommandAsync method for the
        /// closure of the command Poco (the ICommand interface that unifies all other ICommand and <see cref="ICommandPart"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>The <see cref="CommandResult"/>.</returns>
        public Task<CommandResult> ExecuteCommandAsync( IActivityMonitor monitor, IServiceProvider services, ICommand command )
        {
            return ExecuteCommandAsync( monitor, services, Directory.Find( command ) );
        }

        /// <summary>
        /// Executes a command by calling the ExecuteCommand or ExecuteCommandAsync method for the
        /// closure of the command Poco (the ICommand interface that unifies all other ICommand and <see cref="ICommandPart"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>The <see cref="CommandResult"/>.</returns>
        public abstract Task<CommandResult> ExecuteCommandAsync( IActivityMonitor monitor, IServiceProvider services, KnownCommand command );
    }
}
