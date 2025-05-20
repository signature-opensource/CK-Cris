namespace CK.Cris;

/// <summary>
/// Typology of the main <see cref="ICrisPoco"/> types.
/// </summary>
public enum CrisPocoKind
{
    /// <summary>
    /// A <see cref="ICommand"/> without result.
    /// </summary>
    Command,

    /// <summary>
    /// A <see cref="ICommand{TResult}"/>.
    /// </summary>
    CommandWithResult,

    /// <summary>
    /// A <see cref="IEvent"/> that can be observed only
    /// by the source command execution context.
    /// </summary>
    CallerOnlyEvent,

    /// <summary>
    /// An immediate <see cref="IEvent"/> that can be observed only
    /// by the source command execution context.
    /// </summary>
    CallerOnlyImmediateEvent,

    /// <summary>
    /// A <see cref="IEvent"/> that is immediately routed to all routed
    /// event handlers that can handle it.
    /// </summary>
    RoutedImmediateEvent,

    /// <summary>
    /// A <see cref="IEvent"/> that will be routed to all routed
    /// event handlers that can handle it once the command
    /// has been successfully executed.
    /// </summary>
    RoutedEvent
}
