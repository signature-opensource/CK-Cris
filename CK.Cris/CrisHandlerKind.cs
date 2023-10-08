namespace CK.Cris
{
    /// <summary>
    /// There are 4 kind of handlers.
    /// </summary>
    public enum CrisHandlerKind
    {
        /// <summary>
        /// Validates a command.
        /// </summary>
        CommandValidator,

        /// <summary>
        /// Handles a command.
        /// </summary>
        CommandHandler,

        /// <summary>
        /// Called after the <see cref="CommandHandler"/>.
        /// </summary>
        CommandPostHandler,

        /// <summary>
        /// Handles a <see cref="IEvent"/> decorated with a <see cref="RouredEventAttribute"/>.
        /// </summary>
        RoutedEventHandler
    }

}
