using CK.Core;

namespace CK.Cris.EndpointValues
{
    /// <summary>
    /// Defines an extensible set of properties that can be initialized from or need to
    /// be challenged against the endpoint services.
    /// This can be any kind of information like authentication informations, IP address, public keys, etc.
    /// <para>
    /// Commands (generally via <see cref="ICommandPart"/>) can define these properties thanks to the <see cref="EndpointValueAttribute"/>.
    /// </para>
    /// <para>
    /// These properties cannot be null: they necessarily exist and a [PostHandler] method with the
    /// <see cref="IEndpointValuesCollectCommand"/> must set them.
    /// </para>
    /// </summary>
    public interface IEndpointValues : IPoco
    {
    }
}
