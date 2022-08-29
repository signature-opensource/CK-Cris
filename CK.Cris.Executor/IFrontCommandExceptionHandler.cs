using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Handles any exception that occurred during the handling of a command by the <see cref="FrontCommandExecutor"/>.
    /// Default implementation is provided by <see cref="DefaultFrontCommandExceptionHandler"/>.
    /// </summary>
    public interface IFrontCommandExceptionHandler : IAutoService
    {
        /// <summary>
        /// Handle an execution error. Must typically log the exception and the
        /// command and configure the result's error in <see cref="ICrisResult.Result"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="ex">The exception.</param>
        /// <param name="command">The command that failed.</param>
        /// <param name="result">The command result (its <see cref="ICrisResult.Code"/> is already set to <see cref="VESACode.Error"/>).</param>
        /// <returns>The awaitable.</returns>
        ValueTask OnErrorAsync( IActivityMonitor monitor, IServiceProvider services, Exception ex, ICommand command, ICrisResult result );
    }
}
