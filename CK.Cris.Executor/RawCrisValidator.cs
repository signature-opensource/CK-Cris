using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Command validation service.
    /// </summary>
    [CK.Setup.ContextBoundDelegation( "CK.Setup.Cris.RawCrisValidatorImpl, CK.Cris.Executor.Engine" )]
    public abstract class RawCrisValidator : ISingletonAutoService
    {
        protected CrisDirectory Directory;

        /// <summary>
        /// Initializes a new <see cref="RawCrisValidator"/>.
        /// </summary>
        /// <param name="directory">The command directory.</param>
        public RawCrisValidator( CrisDirectory directory )
        {
            Directory = directory;
        }

        /// <summary>
        /// Validates a command by calling all the discovered validators.
        /// <para>
        /// Exceptions are NOT handled by this method: a validator should never throw, exceptions must be handled by the caller.
        /// </para>
        /// </summary>
        /// <param name="validationMonitor">The validation monitor that collects validation results (warnings and errors).</param>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="command">The command to validate.</param>
        /// <returns>The validation result.</returns>
        public abstract Task<CrisValidationResult> ValidateCommandAsync( IActivityMonitor validationMonitor,
                                                                         IServiceProvider services,
                                                                         IAbstractCommand command );
    }
}
