using CK.Cris;

namespace CK.Auth;

/// <summary>
/// Defines the <see cref="ActorId"/> field.
/// This is the most basic authentication related part: the only guaranty is that the actor identifier is the
/// current <see cref="IAuthenticationInfo.UnsafeUser"/>.
/// </summary>
public interface IAuthUnsafePart : ICrisPocoPart
{
    /// <summary>
    /// Gets or sets the actor identifier.
    /// </summary>
    [AmbientServiceValue]
    int? ActorId { get; set; }
}
