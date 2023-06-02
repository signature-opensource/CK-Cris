using System;

namespace CK.Cris
{
    /// <summary>
    /// The concrete <see cref="IEvent"/> can be handled by methods (decorated with <see cref="RoutedEventHandlerAttribute"/>).
    /// This can decorate only the concrete <see cref="IEvent"/> root, not a <see cref="IEventPart"/>
    /// nor an extension.
    /// <para>
    /// Without this attribute, an event is only observable by the command caller (<see cref="CrisPocoKind.CallerOnlyEvent"/>
    /// or <see cref="CrisPocoKind.CallerOnlyImmediateEvent"/>).
    /// </para>
    /// </summary>
    [AttributeUsage( AttributeTargets.Interface, AllowMultiple = false, Inherited = false )]
    public sealed class RoutedEventAttribute : Attribute
    {
    }
}
