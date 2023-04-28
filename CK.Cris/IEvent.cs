using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// An event is a <see cref="ICrisPoco"/> without result
    /// and that can not be awaited when sent: it is a fire and forget command.
    /// Any type that extends this interface defines a new event type.
    /// Event (just like Command) type names should keep the initial "I" (of the interface)
    /// and end with "Event".
    /// </summary>
    public interface IEvent : ICrisPoco
    {
    }

}
