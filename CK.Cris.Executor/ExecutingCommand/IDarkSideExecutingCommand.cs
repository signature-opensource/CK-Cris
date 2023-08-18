using CK.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Is the name clear enough? This is the internal side of a <see cref="IExecutedCommand"/> that
    /// must be used by command executor to applies the execution steps on the command.
    /// </summary>
    public interface IDarkSideExecutingCommand
    {
        /// <summary>
        /// Sets the validation result of a command. There is no "TrySetValidationResult": the validation result must be set once and only once.
        /// If <see cref="CrisValidationResult.Success"/> is false, a non nul <paramref name="validationError"/> is provided and
        /// this immediately completes the execution.
        /// </summary>
        /// <param name="v">The validation result.</param>
        /// <param name="validationError">The <see cref="ICrisResultError"/> built from the validation result or null if the command is valid.</param>
        void SetValidationResult( CrisValidationResult v, ICrisResultError? validationError );

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

