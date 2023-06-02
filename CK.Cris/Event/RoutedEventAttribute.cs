using System;

namespace CK.Cris
{
    /// <summary>
    /// Defines the <see cref="EventRoutingMode"/> of a concrete <see cref="IEvent"/>.
    /// This can decorate only the concrete <see cref="IEvent"/> root, not a <see cref="IEventPart"/>
    /// nor an extension.
    /// <para>
    /// Without this attribute, an event is only observable from its caller (<see cref="CrisPocoKind.Event"/>
    /// </para>
    /// </summary>
    [AttributeUsage( AttributeTargets.Interface, AllowMultiple = false, Inherited = false )]
    public sealed class RoutedEventAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="RoutedEventAttribute"/> with a specified mode.
        /// </summary>
        /// <param name="mode">The routing mode.</param>
        public RoutedEventAttribute( EventRoutingMode mode )
        {
            Mode = mode;
        }

        /// <summary>
        /// Gets the routing mode.
        /// </summary>
        public EventRoutingMode Mode { get; }
    }
}
