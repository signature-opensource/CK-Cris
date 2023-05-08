namespace CK.Cris
{
    /// <summary>
    /// Defines the routing mode of <see cref="RoutedEventAttribute"/>.
    /// </summary>
    public enum EventRoutingMode
    {
        /// <summary>
        /// The event is routed to all routed event handlers that can handle it
        /// once the command has been successfully executed.
        /// </summary>
        OnCommandSuccess,

        /// <summary>
        /// The event is routed to all routed event handlers that can handle it
        /// once the command has been executed even if the execution fails at some point.
        /// </summary>
        OnCommandCompletion,

        /// <summary>
        /// The event is immediately routed to all routed
        /// event handlers that can handle it. 
        /// </summary>
        Immediate,
    }
}
