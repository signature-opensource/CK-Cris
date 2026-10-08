using CK.Core;
using CK.Cris;
using CK.IO.Authentication;
using System.Threading.Tasks;

namespace CK.Auth;

/// <summary>
/// Opens an authentication session from a login command. This is <b>not</b> a <see cref="ILoginProvider"/>:
/// a provider only checks the credentials of its schemes; this service runs the whole login sequence (provider,
/// <see cref="ILoginValidator"/>, account creation or binding) and then creates the session, its token and its
/// endpoint proof (see <see cref="IAuthenticationSessionEndpoint"/>).
/// <para>
/// It is implemented by the authentication center and called by the login command handlers that each authentication
/// provider package defines: "I implement an authentication provider, I handle its login commands". A handler only
/// tells which scheme and payload to use:
/// <code>
/// [CommandHandler]
/// public ValueTask&lt;ISessionResult&gt; HandleAsync( IActivityMonitor monitor, IWeakLoginCommand cmd, IAuthenticationSessionOpener opener )
///     =&gt; opener.OpenSessionAsync( monitor, cmd, "Weak", cmd );
/// </code>
/// </para>
/// <para>
/// It is scoped: it works with the <see cref="IAuthenticationSessionEndpoint"/> that received the command.
/// </para>
/// </summary>
public interface IAuthenticationSessionOpener : IScopedAutoService
{
    /// <summary>
    /// Runs the login and, on success, opens the session. A failed login is not an exception: the result
    /// carries the error and no token.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <typeparam name="T">The login command type.</typeparam>
    /// <param name="command">The login command being handled.</param>
    /// <param name="scheme">The scheme to use (see <see cref="LoginScheme"/>).</param>
    /// <param name="payload">The <see cref="ILoginProvider"/> payload for the scheme.</param>
    /// <param name="rememberMe">Whether the session must survive the end of the caller session.</param>
    /// <returns>The session result.</returns>
    ValueTask<ISessionResult> OpenSessionAsync<T>( IActivityMonitor monitor,
                                                   T command,
                                                   string scheme,
                                                   object payload,
                                                   bool rememberMe = false )
        where T : ICommand<ISessionResult>, ICommandAuthenticationSession;
}
