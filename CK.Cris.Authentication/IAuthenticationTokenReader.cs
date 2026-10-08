using CK.Core;
using System.Threading.Tasks;

namespace CK.Auth;

/// <summary>
/// Reads the bearer tokens issued by the authentication center of the current local party. An endpoint that receives
/// authenticated commands (the HTTP front for instance) uses it to establish their <see cref="AuthenticationInfo"/>.
/// <para>
/// Implemented by the authentication center package: the token is verified locally (no call to the authentication
/// center), with the keys of the authentication center.
/// </para>
/// </summary>
public interface IAuthenticationTokenReader : IScopedAutoService
{
    /// <summary>
    /// Reads a token. Returns null when it is refused (the reason is logged).
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="token">The bearer token.</param>
    /// <returns>The token content or null.</returns>
    ValueTask<AuthenticationTokenData?> ReadTokenAsync( IActivityMonitor monitor, string token );
}

/// <summary>
/// The content of a token read by a <see cref="IAuthenticationTokenReader"/>.
/// </summary>
/// <param name="Info">The authentication.</param>
/// <param name="BindingSecretHash">
/// The hash of the binding secret of the session that issued the token (base64url SHA-256, see <see cref="AuthenticationSessionProof"/>):
/// the endpoint checks that the caller presents this binding secret, so that a stolen token cannot be used elsewhere.
/// </param>
public sealed record AuthenticationTokenData( AuthenticationInfo Info, string BindingSecretHash );
