using CK.Auth;
using CK.Core;
using System.Threading.Tasks;

namespace CK.Testing;

/// <summary>
/// The token side of the fake authentication center: it reads the tokens issued by the <see cref="FakeAuthCenterOperations"/>.
/// </summary>
public class FakeTokenReader : IAuthenticationTokenReader
{
    readonly FakeAuthCenterOperations _center;

    /// <summary>
    /// Initializes a new token reader.
    /// </summary>
    /// <param name="center">The fake authentication center that issues the tokens.</param>
    public FakeTokenReader( FakeAuthCenterOperations center ) => _center = center;

    /// <inheritdoc />
    public virtual ValueTask<AuthenticationTokenData?> ReadTokenAsync( IActivityMonitor monitor, string token )
        => ValueTask.FromResult( _center.FindToken( token ) );
}
