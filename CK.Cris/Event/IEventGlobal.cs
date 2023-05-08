using CK.Core;

namespace CK.Cris
{
    /// <summary>
    /// Captures the full name of the party that emitted the event.
    /// </summary>
    public interface IEventGlobal : IEventPart
    {
        /// <summary>
        /// Gets the <see cref="CoreApplicationIdentity.FullName"/> of the party that emitted
        /// this event. If <see cref="CoreApplicationIdentity.IsInitialized"/> is false, this is
        /// "Undefined/Develop/Unknown".
        /// </summary>
        string PartyFullName { get; set; }
    }
}
