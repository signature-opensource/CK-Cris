using CK.Core;

namespace CK.Cris.AmbientValues
{
    /// <summary>
    /// Defines an extensible set of properties that are always available. Their values come from ambient or
    /// processwisde services.
    /// This can be any kind of information like authentication informations (that is an ambient information),
    /// public keys (that would be managed by a processwide singleton), etc.
    /// <para>
    /// Commands (quite always <see cref="ICommandPart"/>) can define these properties thanks to the <see cref="AmbientServiceValueAttribute"/>.
    /// </para>
    /// <para>
    /// These properties cannot be null: they necessarily exist and a [PostHandler] method with the
    /// <see cref="IAmbientValuesCollectCommand"/> must set them.
    /// </para>
    /// </summary>
    public interface IAmbientValues : IPoco
    {
    }
}
