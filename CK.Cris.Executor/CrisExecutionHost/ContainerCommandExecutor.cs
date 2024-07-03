using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Non generic base class for <see cref="ContainerCommandExecutor{T}"/>.
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
    public abstract class ContainerCommandExecutor
    {
        internal ContainerCommandExecutor() { }

        internal async Task RaiseImmediateEventAsync( IActivityMonitor monitor, CrisJob job, IEvent e )
        {
            if( job._executingCommand != null ) await job._executingCommand.DarkSide.AddImmediateEventAsync( monitor, e );
            await OnImmediateEventAsync( monitor, job, e );
        }

        /// <summary>
        /// Called right before the execution. A scope must be obtained from the container that will host
        /// the execution xor an error must be returned.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="job">The starting job.</param>
        /// <returns>An error xor the configured DI scope to use.</returns>
        internal protected abstract ValueTask<(ICrisResultError?, AsyncServiceScope)> PrepareJobAsync( IActivityMonitor monitor, CrisJob job );

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
        /// (<see cref="SetFinalResultAsync(IActivityMonitor, CrisJob, object?, ImmutableArray{UserMessage}, ImmutableArray{IEvent})"/>) is not called).
        /// </param>
        /// <returns>The awaitable.</returns>
        internal protected virtual Task OnCrisValidationResultAsync( IActivityMonitor monitor, CrisJob job, CrisValidationResult validation ) => Task.CompletedTask;

        /// <summary>
        /// Extension point called when a command emits an immediate event (routed or caller only events).
        /// Note that all local impacts have been already handled: the <see cref="CrisEventHub"/> has already raised the event
        /// and if <see cref="CrisJob.ExecutingCommand"/> is not null, the <see cref="IExecutingCommand.Events"/> have been updated.
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
        /// <param name="result">The executed command.</param>
        /// <returns>The awaitable.</returns>
        internal protected virtual Task SetFinalResultAsync( IActivityMonitor monitor, CrisJob job, IExecutedCommand result )
        {
            return Task.CompletedTask;
        }
    }

}
