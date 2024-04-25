using CK.Cris;

namespace CK.Auth
{
    /// <summary>
    /// Defines the <see cref="ActorId"/> field.
    /// This is the most basic command part: the only guaranty is that the actor identifier is the
    /// current <see cref="IAuthenticationInfo.UnsafeUser"/>.
    /// </summary>
    public interface ICommandAuthUnsafe : ICommandPart
    {
        /// <summary>
        /// Gets or sets the actor identifier.
        /// The default <see cref="CrisAuthenticationService"/> validates this field against the
        /// current <see cref="IAuthenticationInfo.UnsafeUser"/>.
        /// </summary>
        [EndpointValue]
        int? ActorId { get; set; }
    }
}
