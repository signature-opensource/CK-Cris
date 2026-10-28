using CK.Core;
using CK.Cris;
using CK.IO.Authentication;
using CK.IO.Authentication.Weak;
using System.Threading.Tasks;

namespace CK.Auth;

/// <summary>
/// Handles the <see cref="IWeakLoginCommand"/> at the endpoint that receives it: the session is opened by the
/// authentication center (the "Weak" provider is there). The command may be extended (CK.IO.Authentication.WeakZone
/// adds the zone): this handler handles the whole command and the payload carries the extension to the provider.
/// </summary>
public sealed class WeakLoginHandler : ISingletonAutoService
{
    /// <summary>
    /// Opens the session.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="cmd">The login command (the payload of the provider).</param>
    /// <param name="opener">The session opener.</param>
    /// <returns>The session result.</returns>
    [CommandHandler( AllowUnclosedCommand = true )]
    public ValueTask<ISessionResult> HandleAsync( IActivityMonitor monitor, IWeakLoginCommand cmd, IAuthenticationSessionOpener opener )
        => opener.OpenSessionAsync( monitor, cmd, ActorInfo.WeakScheme, cmd );
}
