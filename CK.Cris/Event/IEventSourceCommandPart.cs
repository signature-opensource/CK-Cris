using CK.Core;

namespace CK.Cris;

/// <summary>
/// Captures a reference to the command that emitted the event.
/// </summary>
[CKTypeDefiner]
public interface IEventSourceCommandPart : IEventPart
{
    /// <summary>
    /// Gets the <see cref="IAbstractCommand"/> that emitted this event.
    /// </summary>
    [NullInvalid]
    IAbstractCommand? SourceCommand { get; set; }
}
