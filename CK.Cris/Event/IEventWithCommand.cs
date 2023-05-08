namespace CK.Cris
{
    /// <summary>
    /// Captures a reference to the command that emitted the event.
    /// </summary>
    public interface IEventWithCommand : IEventPart
    {
        /// <summary>
        /// Gets the <see cref="IAbstractCommand"/> that emitted this event.
        /// </summary>
        IAbstractCommand SourceCommand { get; set; }
    }
}
