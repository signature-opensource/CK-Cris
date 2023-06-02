using CK.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Is the name clear enough? This is the internal side of a <see cref="IExecutedCommand"/> that
    /// must be used by command executor to translate the execution steps on the command.
    /// </summary>
    public interface IDarkSideExecutingCommand
    {
        /// <summary>
        /// Sets the validation result of a command. There is no "TrySetValidationResult": the validation result must be set once and only once.
        /// If the validation fails, this immediately completes the request and this returns true.
        /// </summary>
        /// <param name="errorFactory">The factory for error.</param>
        /// <param name="v">The validation result.</param>
        /// <returns>True if the validation failed: this command is completed.</returns>
        bool SetValidationResult( IPocoFactory<ICrisResultError> errorFactory, CrisValidationResult v );

        /// <summary>
        /// Adds an immediate event to the <see cref="IExecutedCommand.ImmediateEvents"/> collector.
        /// The <see cref="ImmediateEvents.Added"/> event is immediately raised.
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="e">The event raised by the executing command (or by a subordinate called command).</param>
        /// <returns></returns>
        Task AddImmediateEventAsync( IActivityMonitor monitor, IEvent e );

        /// <summary>
        /// Sets the final result. There is no "TrySetFinalResult": the result must be set once and only once.
        /// </summary>
        /// <param name="events">Events emitted by the command if the command succeeded. Always empty on error.</param>
        /// <param name="result">The request result. Null for command without result.</param>
        void SetResult( IReadOnlyList<IEvent> events, object? result );

        /// <summary>
        /// Sets an execution error. The <see cref="IExecutingCommand.Completion"/> task is faulted.
        /// </summary>
        /// <param name="ex">The execution exception.</param>
        void SetException( Exception ex );

    }

}

