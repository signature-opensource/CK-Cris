using CK.Core;
using CK.IO.Authentication;
using System.Threading.Tasks;

namespace CK.Auth;

/// <summary>
/// Relays the session commands to a remote authentication center. It is called by the endpoints that accept the session
/// commands (<see cref="ICommandAuthenticationSession"/>), <b>before</b> the command handlers: a front has no login
/// providers.
/// <para>
/// Implemented by the "Remote" package of the authentication center: an application is either an authentication center
/// (it has the handlers, no relay) or a front (it has the relay, every session command is relayed). Without
/// implementation, there is no relay.
/// </para>
/// </summary>
public interface IAuthenticationSessionRelay : IScopedAutoService
{
    /// <summary>
    /// Relays the command to the remote authentication center of the current local party. It fails closed: when there is
    /// none, or when it is unavailable or unreachable, the result is an error and the command is never executed here.
    /// <para>
    /// The relayed command is executed by the remote authentication center. What it issues or clears is applied to
    /// the <see cref="IAuthenticationSessionEndpoint"/> before the task completes. The task's result is the command result.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The session command.</param>
    /// <returns>The command result.</returns>
    Task<object?> RelayAsync( IActivityMonitor monitor, ICommandAuthenticationSession command );
}
