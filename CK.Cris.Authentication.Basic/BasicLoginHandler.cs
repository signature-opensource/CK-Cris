using CK.Core;
using CK.Cris;
using CK.IO.Authentication;
using CK.IO.Authentication.Basic;
using System.Threading.Tasks;

namespace CK.Auth;

/// <summary>
/// Handles the <see cref="IBasicLoginCommand"/> at the endpoint that receives it: the session is opened by the
/// authentication center (the "Basic" provider checks the password there).
/// </summary>
public sealed class BasicLoginHandler : ISingletonAutoService
{
    /// <summary>
    /// The scheme of the Basic login.
    /// </summary>
    public const string Scheme = "Basic";

    /// <summary>
    /// Opens the session.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="cmd">The login command (the payload of the provider).</param>
    /// <param name="opener">The session opener.</param>
    /// <returns>The session result.</returns>
    [CommandHandler]
    public ValueTask<ISessionResult> HandleAsync( IActivityMonitor monitor, IBasicLoginCommand cmd, IAuthenticationSessionOpener opener )
        => opener.OpenSessionAsync( monitor, cmd, Scheme, cmd, cmd.RememberMe );
}
