using CK.Core;

namespace CK.Cris;

/// <summary>
/// There are 5 kind of handlers.
/// </summary>
public enum CrisHandlerKind
{
    /// <summary>
    /// Validates an incoming command, event or part (when the Cris Poco is received).
    /// </summary>
    IncomingValidator,

    /// <summary>
    /// Configures the <see cref="AmbientServiceHub"/> from a command, an event or a part.
    /// </summary>
    ConfigureAmbientServices,

    /// <summary>
    /// Restores a <see cref="AmbientServiceHub"/> from a command, an event or a part with the help
    /// of singletons services.
    /// </summary>
    RestoreAmbientServices,

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
    /// Handles a <see cref="IEvent"/> decorated with a <see cref="RoutedEventAttribute"/>.
    /// </summary>
    RoutedEventHandler
}
