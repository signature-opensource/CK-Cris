using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Cris
{
    public interface ICrisCallContext
    {
        /// <summary>
        /// Gets the monitor that can be used to log activities.
        /// </summary>
        IActivityMonitor Monitor { get; }

        /// <summary>
        /// Executes a command in this context.
        /// This must be called sequentially, the returned task must be awaited: no
        /// parallel is supported.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <returns>The command result if any.</returns>
        Task<object?> ExecuteCommandAsync( IAbstractCommand command );

        /// <summary>
        /// Configures and executes a command in this context.
        /// This must be called sequentially, the returned task must be awaited: no
        /// parallel is supported.
        /// </summary>
        /// <param name="configure">A function that configures the command.</param>
        /// <returns>The command result if any.</returns>
        Task<object?> ExecuteCommandAsync<T>( Action<T> configure ) where T : IAbstractCommand;
    }
}
