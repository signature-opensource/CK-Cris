using CK.Core;

namespace CK.Cris;

/// <summary>
/// Marker interface to define mixable event parts.
/// <para>
/// Since event parts de facto defines an event object, their name should start 
/// with "IEvent" in order to distinguish them from actual events that
/// should be suffixed with "Event".
/// </para>
/// </summary>
/// <remarks>
/// Parts can also be extended: when defining a specialized part that extends an
/// existing <see cref="IEventPart"/>, the <see cref="CKTypeDefinerAttribute"/> must be
/// applied to the specialized part.
/// </remarks>
[CKTypeSuperDefiner]
public interface IEventPart : IEvent, ICrisPocoPart
{
}
