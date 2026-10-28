using CK.Core;
using CK.Cris;
using CK.IO.Authentication;
using System.Threading.Tasks;

namespace CK.Auth;

/// <summary>
/// The handlers of the session commands shared by all the schemes. They are executed by the endpoint that received
/// the commands, on the authentication center as on any other party: they only call the
/// <see cref="AuthenticationSessionEndpointService"/>, whose operations are handled by the authentication center.
/// The login commands of the schemes are handled by their own packages (CK.Cris.Authentication.Basic, .Weak, .Passkey).
/// </summary>
public sealed class AuthenticationSessionHandlers : ISingletonAutoService
{
    /// <summary>
    /// Refreshes the session.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command.</param>
    /// <param name="service">The endpoint service.</param>
    /// <returns>The session result.</returns>
    [CommandHandler]
    public ValueTask<ISessionResult> HandleAsync( IActivityMonitor monitor, IRefreshSessionCommand command, AuthenticationSessionEndpointService service )
        => service.RefreshAsync( monitor );

    /// <summary>
    /// Revokes the session: the endpoint's secrets are cleared whatever happens.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command.</param>
    /// <param name="service">The endpoint service.</param>
    /// <returns>The awaitable.</returns>
    [CommandHandler]
    public ValueTask HandleAsync( IActivityMonitor monitor, ILogoutSessionCommand command, AuthenticationSessionEndpointService service )
        => service.LogoutAsync( monitor );

    /// <summary>
    /// Impersonates.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command.</param>
    /// <param name="service">The endpoint service.</param>
    /// <returns>The session result.</returns>
    [CommandHandler]
    public ValueTask<ISessionResult> HandleAsync( IActivityMonitor monitor, IImpersonateCommand command, AuthenticationSessionEndpointService service )
        => service.ImpersonateAsync( monitor, command );

    /// <summary>
    /// Clears the impersonation.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command.</param>
    /// <param name="service">The endpoint service.</param>
    /// <returns>The session result.</returns>
    [CommandHandler]
    public ValueTask<ISessionResult> HandleAsync( IActivityMonitor monitor, IClearImpersonationCommand command, AuthenticationSessionEndpointService service )
        => service.ClearImpersonationAsync( monitor );

    /// <summary>
    /// Starts a remote login.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command.</param>
    /// <param name="service">The endpoint service.</param>
    /// <returns>The URL to which the caller is redirected.</returns>
    [CommandHandler]
    public ValueTask<RedirectedUrl> HandleAsync( IActivityMonitor monitor, IStartLoginCommand command, AuthenticationSessionEndpointService service )
        => service.StartRemoteLoginAsync( monitor, command );

    /// <summary>
    /// Completes a remote login.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command built by the endpoint's callback.</param>
    /// <param name="service">The endpoint service.</param>
    /// <returns>The session result.</returns>
    [CommandHandler]
    public ValueTask<ISessionResult> HandleAsync( IActivityMonitor monitor, IRemoteLoginCallbackCommand command, AuthenticationSessionEndpointService service )
        => service.CompleteRemoteLoginAsync( monitor, command );
}
