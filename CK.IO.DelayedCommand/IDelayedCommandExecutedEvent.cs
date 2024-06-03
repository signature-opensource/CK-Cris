using CK.Core;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CK.Cris
{
    /// <summary>
    /// Event raised once a <see cref="IDelayedCommand"/> has been executed.
    /// </summary>
    [RoutedEvent]
    public interface IDelayedCommandExecutedEvent : IEventSourceCommandPart, IPocoCommandExecutedPart, IEvent
    {
    }
}
