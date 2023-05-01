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
        protected CommandDirectory Directory;

        /// <summary>
        /// Initializes a new <see cref="RawCrisValidator"/>.
        /// </summary>
        /// <param name="directory">The command directory.</param>
        public RawCrisValidator( CommandDirectory directory )
        {
            Directory = directory;
        }

        /// <summary>
        /// Validates a command or an event by calling all the discovered validators.
        /// <para>
        /// Exceptions are NOT handled by this method: a validator should never throw, exceptions must be handled by the caller.
        /// </para>
        /// <para>
        /// A <see cref="ICrisEventSender"/> must be resolvable from the <paramref name="services"/>.
        /// </para>
        /// </summary>
        /// <param name="validationMonitor">The validation monitor that collects validation results (warnings and errors).</param>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="o">The command or event to validate.</param>
        /// <returns>The validation result.</returns>
        public abstract Task<CrisValidationResult> ValidateCrisPocoAsync( IActivityMonitor validationMonitor,
                                                                          IServiceProvider services,
                                                                          ICrisPoco o );
    }
}
