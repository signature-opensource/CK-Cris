using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Cris
{
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
        Event,

        /// <summary>
        /// A <see cref="IEvent"/> that is immediately routed to all routed
        /// event handlers that can handle it.
        /// </summary>
        RoutedEventImmediate,

        /// <summary>
        /// A <see cref="IEvent"/> that will be routed to all routed
        /// event handlers that can handle it once the source command
        /// has been successfully executed.
        /// </summary>
        RoutedEventOnSuccess,

        /// <summary>
        /// A <see cref="IEvent"/> that will be routed to all routed
        /// event handlers that can handle it once the source command
        /// has been executed even if the execution fails at some point.
        /// </summary>
        RoutedEventDeferred,
    }
}
