using CK.Core;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CK.Cris
{
    [RoutedEvent]
    public interface IDelayedCommandExecutedEvent : IPocoCommandExecutedPart, IEvent
    {
        IDelayedCommand DelayedCommand { get; }
    }
}
