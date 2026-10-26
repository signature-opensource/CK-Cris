using CK.Core;
using System.Threading.Tasks;

namespace CK.Auth;

/// <summary>
/// Relays the commands executed by the authentication center (<see cref="ICommandAuthCenter"/>) to a remote
/// authentication center. It is called by the endpoints that accept these commands, <b>after</b> their incoming
/// validation and <b>before</b> the command handlers: a front has no handler for them (it has no database), and a
/// local handler must not run for a delegated local party.
/// <para>
/// The relay carries the caller's proof (its token, its binding), never only its actor identifier: the authentication
/// center establishes the identity again. Implemented by the authentication center package. Without implementation,
/// there is no relay.
/// </para>
/// </summary>
public interface IAuthCenterCommandRelay : IScopedAutoService
{
    /// <summary>
    /// Relays the command when the authentication center of the current local party is remote. Returns null when the
    /// command must be executed here (the authentication center is local, or there is none): the endpoint then executes
    /// it as usual. The task's result is the command result (an error result when the relay fails: it fails closed).
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command.</param>
    /// <returns>Null, or the relayed execution and its result.</returns>
    Task<object?>? TryRelay( IActivityMonitor monitor, ICommandAuthCenter command );
}
