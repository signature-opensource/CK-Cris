using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Non generic base class for <see cref="EndpointCommandExecutor{T}"/>.
    /// A <see cref="CrisExecutionHost"/> relies on an executor to:
    /// <list type="bullet">
    ///   <item>
    ///   Create a <see cref="AsyncServiceScope"/> to handle command validation and command execution.
    ///   </item>
    ///   <item>
    ///   Expose the execution impacts to the external world thanks to: <see cref="OnCrisValidationResultAsync"/>, <see cref="OnImmediateEventAsync"/>
    ///   and <see cref="SetFinalResultAsync"/> extension points.
    ///   </item>
    /// </list>
    /// </summary>
    public abstract class EndpointCommandExecutor
    {
        internal EndpointCommandExecutor() { }

        internal abstract AsyncServiceScope CreateAsyncScope( CrisJob job );

        internal async Task RaiseImmediateEventAsync( IActivityMonitor monitor, CrisJob job, IEvent e )
        {
            if( job._executingCommand != null ) await job._executingCommand.DarkSide.AddImmediateEventAsync( monitor, e );
            await OnImmediateEventAsync( monitor, job, e );
        }

        /// <summary>
        /// Extension point called when a command has been validated.
        /// This is always called: this signals the start of a command handling.
        /// <para>
        /// Does nothing by default.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="job">The executing job.</param>
        /// <param name="validation">
        /// The validation result. When <see cref="CrisValidationResult.Success"/> is false, this ends the command handling
        /// (<see cref="OnFinalResultAsync(IActivityMonitor, CrisJob, IReadOnlyList{IEvent}, CrisExecutionHost.ICrisJobResult)"/>) is not called).
        /// </param>
        /// <returns>The awaitable.</returns>
        internal protected virtual Task OnCrisValidationResultAsync( IActivityMonitor monitor, CrisJob job, CrisValidationResult validation ) => Task.CompletedTask;

        /// <summary>
        /// Extension point called when a command emits an immediate event (routed or caller only events).
        /// Note that all local impacts have been already handled: the <see cref="CrisEventHub"/> has already raised the event
        /// and if <see cref="CrisJob.HasExecutingCommand"/> is true, the <see cref="IExecutingCommand.Events"/> have been updated.
        /// <para>
        /// Does nothing by default.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="job">The executing job.</param>
        /// <param name="e">The event.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task OnImmediateEventAsync( IActivityMonitor monitor, CrisJob job, IEvent e ) => Task.CompletedTask;

        /// <summary>
        /// Extension point called once a command has been executed.
        /// Note that all local impacts have been already handled: the <see cref="CrisEventHub"/> has already raised all the events.
        /// <para>
        /// Does nothing by default.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="job">The executed job.</param>
        /// <param name="events">Non empty events raised by the command execution.</param>
        /// <param name="r">The final command result.</param>
        /// <returns>The awaitable.</returns>
        internal protected virtual Task SetFinalResultAsync( IActivityMonitor monitor, CrisJob job, IReadOnlyList<IEvent> events, CrisExecutionHost.ICrisJobResult r ) => Task.CompletedTask;

    }

}
