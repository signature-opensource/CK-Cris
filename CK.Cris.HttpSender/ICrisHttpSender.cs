using CK.AppIdentity;
using CK.Core;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Cris.HttpSender
{
    /// <summary>
    /// Supports sending Cris commands to "<see cref="IRemoteParty.Address"/>/.cris/net" endpoint.
    /// </summary>
    public interface ICrisHttpSender
    {
        /// <summary>
        /// Gets the remote that handles the commands.
        /// </summary>
        IRemoteParty Remote {  get; }

        /// <summary>
        /// Sends a Cris command on the remote endpoint, and returns the <see cref="IExecutedCommand{T}"/>.
        /// This never throws.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to send.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="lineNumber">Calling line number (set by Roslyn).</param>
        /// <param name="fileName">Calling file path (set by Roslyn).</param>
        /// <returns>The <see cref="IExecutedCommand"/>.</returns>
        Task<IExecutedCommand<T>> SendAsync<T>( IActivityMonitor monitor,
                                                T command,
                                                CancellationToken cancellationToken = default,
                                                [CallerLineNumber] int lineNumber = 0,
                                                [CallerFilePath] string? fileName = null )
            where T : class, IAbstractCommand;

        /// <summary>
        /// Sends a Cris command on the remote endpoint, and returns a successful result or throws:
        /// if <see cref="IExecutedCommand.Result"/> is a <see cref="ICrisResultError"/>, this throws.
        /// <para>
        /// This can be used for <see cref="ICommand{TResult}"/> and <see cref="ICommand"/> (without result).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to send.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="lineNumber">Calling line number (set by Roslyn).</param>
        /// <param name="fileName">Calling file path (set by Roslyn).</param>
        /// <returns>The <see cref="IExecutedCommand"/>.</returns>
        Task<IExecutedCommand<T>> SendOrThrowAsync<T>( IActivityMonitor monitor,
                                                       T command,
                                                       CancellationToken cancellationToken = default,
                                                       [CallerLineNumber] int lineNumber = 0,
                                                       [CallerFilePath] string? fileName = null )
            where T : class, IAbstractCommand;

        /// <summary>
        /// Sends a <see cref="ICommand{TResult}"/> on the remote endpoint, and returns its result or throws.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to send.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="lineNumber">Calling line number (set by Roslyn).</param>
        /// <param name="fileName">Calling file path (set by Roslyn).</param>
        /// <returns>The <see cref="IExecutedCommand"/>.</returns>
        Task<TResult> SendAndGetResultOrThrowAsync<TResult>( IActivityMonitor monitor,
                                                             ICommand<TResult> command,
                                                             CancellationToken cancellationToken = default,
                                                             [CallerLineNumber] int lineNumber = 0,
                                                             [CallerFilePath] string? fileName = null );

    }
}
