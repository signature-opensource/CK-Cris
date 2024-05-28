using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Is the name clear enough? This is the internal side of a <see cref="IExecutingCommand"/> that
    /// must be used by command executor to applies the execution steps on the command.
    /// </summary>
    public interface IDarkSideExecutingCommand
    {
        /// <summary>
        /// Adds an immediate event to the <see cref="IExecutedCommand.ImmediateEvents"/> collector.
        /// The <see cref="ImmediateEvents.Added"/> event is immediately raised.
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="e">The event raised by the executing command (or by a subordinate called command).</param>
        /// <returns></returns>
        Task AddImmediateEventAsync( IActivityMonitor monitor, IEvent e );

        /// <summary>
        /// Sets the final result.
        /// There is no "TrySetResult": the result must be set once and only once.
        /// </summary>
        /// <param name="result">The executed command.</param>
        void SetResult( IExecutedCommand result );

    }

}

