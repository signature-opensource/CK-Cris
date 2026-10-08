using CK.Core;
using CK.IO.Authentication;
using System.Threading.Tasks;

namespace CK.Auth;

/// <summary>
/// Relays the session commands to a remote authentication center. It is called by the endpoints that accept the session
/// commands (<see cref="ICommandAuthenticationSession"/>), <b>before</b> the command handlers: a front may have no login
/// providers at all, and its local handlers must not run for a delegated local party.
/// <para>
/// Implemented by the authentication center package. Without implementation, there is no relay.
/// </para>
/// </summary>
public interface IAuthenticationSessionRelay : IScopedAutoService
{
    /// <summary>
    /// Relays the command when the authentication center of the current local party is remote. Returns null when the
    /// command must be executed here (the authentication center is local, or there is none): the endpoint then executes it
    /// as usual.
    /// <para>
    /// The relayed command is executed by the remote authentication center. What it issues or clears is applied to
    /// the <see cref="IAuthenticationSessionEndpoint"/> before the task completes. The task's result is the command result.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The session command.</param>
    /// <returns>Null, or the relayed execution and its result.</returns>
    Task<object?>? TryRelay( IActivityMonitor monitor, ICommandAuthenticationSession command );
}
