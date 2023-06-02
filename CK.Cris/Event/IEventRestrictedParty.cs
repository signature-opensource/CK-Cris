using CK.Core;

namespace CK.Cris
{
    /// <summary>
    /// Captures the full name of the party that emitted the event.
    /// These events should be dispatched only to the corresponding party.
    /// </summary>
    public interface IEventRestrictedParty : IEventPart
    {
        /// <summary>
        /// Gets the <see cref="CoreApplicationIdentity.FullName"/> of the party that emitted
        /// this event. If <see cref="CoreApplicationIdentity.IsInitialized"/> is false, this is
        /// "Undefined/Develop/Unknown".
        /// </summary>
        string PartyFullName { get; set; }
    }

}
