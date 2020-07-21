using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Command validation service.
    /// </summary>
    [CK.Setup.ContextBoundDelegation( "CK.Setup.Cris.CommandValidatorImpl, CK.Cris.Executor.Engine" )]
    public abstract class CommandValidator : ISingletonAutoService
    {
        protected CommandDirectory Directory;

        /// <summary>
        /// Initializes a new <see cref="CommandValidator"/>.
        /// </summary>
        /// <param name="directory">The command directory.</param>
        public CommandValidator( CommandDirectory directory )
        {
            Directory = directory;
        }

        /// <summary>
        /// Validates a command by calling all the ValidateCommand or ValidateCommandAsync methods for all the parts
        /// of the command (<see cref="ICommand"/> and <see cref="ICommandPart"/>).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="command">The command to validate.</param>
        /// <returns>The validation result.</returns>
        public abstract Task<ValidationResult> ValidateCommandAsync( IActivityMonitor monitor, IServiceProvider services, ICommand command );
    }
}
