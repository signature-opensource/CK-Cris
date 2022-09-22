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
        readonly IPocoFactory<ICrisResultError> _simpleErrorResultFactory;

        /// <summary>
        /// Initializes a new <see cref="CommandValidator"/>.
        /// </summary>
        /// <param name="directory">The command directory.</param>
        /// <param name="simpleErrorResultFactory">SimpleValidationError factory.</param>
        public CommandValidator( CommandDirectory directory, IPocoFactory<ICrisResultError> simpleErrorResultFactory )
        {
            Directory = directory;
            _simpleErrorResultFactory = simpleErrorResultFactory;
        }

        /// <summary>
        /// Creates a <see cref="ICrisResultError"/> with the errors if the result is not successful,
        /// null otherwise.
        /// </summary>
        /// <param name="v">The validation result.</param>
        /// <returns>Null or a simple validation result.</returns>
        public ICrisResultError? CreateSimpleErrorResult( ValidationResult v )
        {
            if( v.Success ) return null;
            var r = _simpleErrorResultFactory.Create();
            r.Errors.AddRange( v.Errors );
            return r;
        }

        /// <summary>
        /// Validates a command by calling all the ValidateCommand or ValidateCommandAsync methods for all the parts
        /// of the command (<see cref="ICommand"/> and <see cref="ICommandPart"/>).
        /// <para>
        /// Exceptions are NOT handled by this method: a validator should never throw: exceptions must be handled by the caller.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="command">The command to validate.</param>
        /// <returns>The validation result.</returns>
        public abstract Task<ValidationResult> ValidateCommandAsync( IActivityMonitor monitor, IServiceProvider services, ICommand command );
    }
}
