using CK.Cris;

namespace CK.Auth;

/// <summary>
/// Captures the authentication info and the authentication token (that
/// contains the encrypted information).
/// </summary>
public interface IAuthenticationResult : IStandardResultPart
{
    /// <summary>
    /// Gets the authentication info.
    /// </summary>
    IPocoAuthenticationInfo Info { get; }

    /// <summary>
    /// Gets or sets the authentication token.
    /// </summary>
    string? Token { get; set; }
}
