namespace CK.Cris
{
    /// <summary>
    /// There are 5 kind of handlers.
    /// </summary>
    public enum CrisHandlerKind
    {
        /// <summary>
        /// Validates a command syntaxically (when the command is received).
        /// </summary>
        CommandIncomingValidator,

        /// <summary>
        /// Validates a command right before it is handled.
        /// </summary>
        CommandHandlingValidator,

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
