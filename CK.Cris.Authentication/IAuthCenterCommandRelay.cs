using CK.Core;
using System.Threading.Tasks;

namespace CK.Auth;

/// <summary>
/// Relays the commands executed by the authentication center (<see cref="ICommandAuthCenter"/>) to a remote
/// authentication center. It is called by the endpoints that accept these commands, <b>after</b> their incoming
/// validation and <b>before</b> the command handlers: a front has no handler for them (it has no database).
/// <para>
/// The relay carries the caller's proof (its token, its binding), never only its actor identifier: the authentication
/// center establishes the identity again. Implemented by the "Remote" package of the authentication center (see
/// <see cref="IAuthenticationSessionRelay"/>). Without implementation, there is no relay.
/// </para>
/// </summary>
public interface IAuthCenterCommandRelay : IScopedAutoService
{
    /// <summary>
    /// Relays the command to the remote authentication center of the current local party. The task's result is the
    /// command result (an error result when the relay fails: it fails closed, the command is never executed here).
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command.</param>
    /// <returns>The command result.</returns>
    Task<object?> RelayAsync( IActivityMonitor monitor, ICommandAuthCenter command );
}
