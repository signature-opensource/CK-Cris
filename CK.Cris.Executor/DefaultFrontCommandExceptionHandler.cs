using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Default <see cref="FrontCommandExecutor"/> error handler.
    /// This is a template method that can be specialized but can also be totally replaced
    /// (see <see cref="ReplaceAutoServiceAttribute"/>).
    /// </summary>
    public class DefaultFrontCommandExceptionHandler : IFrontCommandExceptionHandler
    {
        /// <summary>
        /// Handles the error by logging the exception, calling <see cref="DumpCommand(IActivityMonitor, IServiceProvider, ICommand)"/>
        /// to dump the command and setting <see cref="ICrisResult.Result"/> to the exception message.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="ex">The exception.</param>
        /// <param name="command">The command that failed.</param>
        /// <param name="result">The command result (its <see cref="ICrisResult.Code"/> is already set to <see cref="VESACode.Error"/>).</param>
        /// <returns>The awaitable.</returns>
        public virtual ValueTask OnErrorAsync( IActivityMonitor monitor, IServiceProvider services, Exception ex, ICommand command, ICrisResult result )
        {
            using( monitor.OpenError( $"While handling command '{command.CommandModel.CommandName}'.", ex ) )
            {
                DumpCommand( monitor, services, command );
            }
            result.Result = ex.Message;
            return default;
        }

        /// <summary>
        /// Dumps the command detail in the <paramref name="monitor"/>.
        /// By default, this sends the ToString() representation of the command as trace.
        /// (That will be handled by the monitor since it is in an OpenError context.
        /// See the remarks in <see cref="IActivityMonitor.UnfilteredOpenGroup(ActivityMonitorGroupData)"/>).
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="services">The services.</param>
        /// <param name="command">The command that failed.</param>
        protected virtual void DumpCommand( IActivityMonitor monitor, IServiceProvider services, ICommand command )
        {
            monitor.Trace( command.ToString()! );
        }
    }
}
