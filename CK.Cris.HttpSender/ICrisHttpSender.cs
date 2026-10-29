using CK.AppIdentity;
using CK.Core;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Cris.HttpSender;

/// <summary>
/// Supports sending Cris commands to "<see cref="IRemoteParty.Address"/>/.cris/net" endpoint.
/// </summary>
public interface ICrisHttpSender
{
    /// <summary>
    /// Gets the remote that handles the commands.
    /// </summary>
    IRemoteParty Remote { get; }

    /// <summary>
    /// Gets or sets whether the session commands (<see cref="CK.IO.Authentication.ICommandAuthenticationSession"/>) automatically
    /// update the <see cref="AuthorizationToken"/>: the token of a successful <see cref="CK.IO.Authentication.ISessionResult"/>
    /// (a login, a refresh) is set, a <see cref="CK.IO.Authentication.ILogoutSessionCommand"/> or a failed
    /// <see cref="CK.IO.Authentication.IRefreshSessionCommand"/> clears it.
    /// <para>
    /// Defaults to false.
    /// </para>
    /// </summary>
    public bool SkipAutomaticAuthorizationToken { get; set; }

    /// <summary>
    /// Gets or sets an optional "Authorization" bearer token.
    /// <para>
    /// <see cref="SkipAutomaticAuthorizationToken"/> should be set to true when using this directly.
    /// </para>
    /// </summary>
    string? AuthorizationToken { get; set; }

    /// <summary>
    /// Sends a Cris command on the remote endpoint, and returns the <see cref="IExecutedCommand{T}"/>.
    /// This never throws.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command to send.</param>
    /// <param name="timeout">Optional timeout that will override the configured "Timeout" (that defaults to 100 seconds).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <param name="lineNumber">Calling line number (set by Roslyn).</param>
    /// <param name="fileName">Calling file path (set by Roslyn).</param>
    /// <returns>The <see cref="IExecutedCommand"/>.</returns>
    Task<IExecutedCommand<T>> SendAsync<T>( IActivityMonitor monitor,
                                            T command,
                                            TimeSpan? timeout = null,
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
    /// <param name="timeout">Optional timeout that will override the configured "Timeout" (that defaults to 100 seconds).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <param name="lineNumber">Calling line number (set by Roslyn).</param>
    /// <param name="fileName">Calling file path (set by Roslyn).</param>
    /// <returns>The <see cref="IExecutedCommand"/>.</returns>
    Task<IExecutedCommand<T>> SendOrThrowAsync<T>( IActivityMonitor monitor,
                                                   T command,
                                                   TimeSpan? timeout = null,
                                                   CancellationToken cancellationToken = default,
                                                   [CallerLineNumber] int lineNumber = 0,
                                                   [CallerFilePath] string? fileName = null )
        where T : class, IAbstractCommand;

    /// <summary>
    /// Sends a <see cref="ICommand{TResult}"/> on the remote endpoint, and returns its result or throws.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command to send.</param>
    /// <param name="timeout">Optional timeout that will override the configured "Timeout" (that defaults to 100 seconds).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <param name="lineNumber">Calling line number (set by Roslyn).</param>
    /// <param name="fileName">Calling file path (set by Roslyn).</param>
    /// <returns>The <see cref="IExecutedCommand"/>.</returns>
    Task<TResult> SendAndGetResultOrThrowAsync<TResult>( IActivityMonitor monitor,
                                                         ICommand<TResult> command,
                                                         TimeSpan? timeout = null,
                                                         CancellationToken cancellationToken = default,
                                                         [CallerLineNumber] int lineNumber = 0,
                                                         [CallerFilePath] string? fileName = null );

}
