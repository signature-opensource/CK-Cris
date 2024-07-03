using CK.Core;

namespace CK.Cris
{
    /// <summary>
    /// Marker interface to define mixable command or event parts.
    /// <para>
    /// Parts defined from this type instead of <see cref="ICommandPart"/> or <see cref="IEventPart"/>
    /// apply to commands as well as events. They should be suffixed by "Part".
    /// </para>
    /// </summary>
    /// <remarks>
    /// Parts can also be extended: when defining a specialized part that extends an
    /// existing <see cref="ICrisPocoPart"/>, the <see cref="CKTypeDefinerAttribute"/> must be
    /// applied to the specialized part.
    /// </remarks>
    [CKTypeSuperDefiner]
    public interface ICrisPocoPart : ICrisPoco
    {
    }

}
