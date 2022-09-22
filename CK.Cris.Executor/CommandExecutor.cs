using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Primary command executor.
    /// It is currently a simple relay to the <see cref="FrontCommandExecutor"/>.
    /// </summary>
    public class CommandExecutor : ISingletonAutoService
    {
        readonly CommandDirectory _directory;
        readonly FrontCommandExecutor _frontExecutor;
        readonly IPocoFactory<ICrisResultError> _errorResultFactory;

        /// <summary>
        /// Initializes a new <see cref="CommandExecutor"/>.
        /// </summary>
        /// <param name="directory">The command directory.</param>
        /// <param name="frontExecutor">The front executor.</param>
        /// <param name="errorResultFactory">The error result factory.</param>
        public CommandExecutor( CommandDirectory directory, FrontCommandExecutor frontExecutor, IPocoFactory<ICrisResultError> errorResultFactory )
        {
            _directory = directory;
            _frontExecutor = frontExecutor;
            _errorResultFactory = errorResultFactory;
        }

        /// <summary>
        /// Executes a command by calling the ExecuteCommand or ExecuteCommandAsync method for the
        /// closure of the command Poco (the ICommand interface that unifies all other ICommand and <see cref="ICommandPart"/>.
        /// Any exceptions are caught and sent to the <see cref="IFrontCommandExceptionHandler"/> service.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>The <see cref="ICrisResult"/>.</returns>
        public Task<ICrisResult> ExecuteCommandAsync( IActivityMonitor monitor, IServiceProvider services, ICommand command )
        {
            return _frontExecutor.ExecuteCommandAsync( monitor, services, command );
        }

        /// <summary>
        /// Creates a <see cref="ICrisResultError"/> with at least one error.
        /// </summary>
        /// <param name="firstError">The required first error.</param>
        /// <param name="otherErrors">Optional other errors (null strings are ignored).</param>
        /// <returns>A simple validation result.</returns>
        public ICrisResultError CreateErrorResult( string firstError, params string?[] otherErrors )
        {
            return _errorResultFactory.Create( firstError, otherErrors );
        }

    }
}
