namespace CK.Cris
{
    /// <summary>
    /// Extends <see cref="IEventRestrictedParty"/> to lessen the restriction of the event:
    /// these events should be dispatched to all parties of a domain.
    /// </summary>
    public interface IEventRestrictedDomain : IEventRestrictedParty
    {
    }

}
