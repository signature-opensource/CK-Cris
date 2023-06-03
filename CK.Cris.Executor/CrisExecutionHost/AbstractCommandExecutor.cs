using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Base class for command executors. A <see cref="CrisExecutionHost"/> relies on an executor to:
    /// <list type="bullet">
    ///   <item>
    ///   Create a <see cref="AsyncServiceScope"/> to handle command validation and command execution.
    ///   </item>
    ///   <item>
    ///   Expose the execution impacts to the external world thanks to: <see cref="OnCrisValidationResultAsync"/>, <see cref="OnImmediateEventAsync"/>
    ///   and <see cref="OnFinalResultAsync"/> extension points.
    ///   </item>
    /// </list>
    /// A concrete executor must be bound to a <see cref="EndpointDefinition"/>. It uses a specialized <see cref="CrisJob"/> to both
    /// configure the execution DI container and to capture specific data that can be used to send execution results back to a caller.
    /// </summary>
    [CKTypeDefiner]
    public abstract class AbstractCommandExecutor : ISingletonAutoService
    {
        readonly DarkSideCrisEventHub _hub;

        public AbstractCommandExecutor( DarkSideCrisEventHub hub )
        {
            _hub = hub;
        }

        internal async Task RaiseImmediateEventAsync( IActivityMonitor monitor, CrisJob job, IEvent e )
        {
            if( job._executingCommand != null ) await job._executingCommand.DarkSide.AddImmediateEventAsync( monitor, e );
            await _hub.ImmediateSender.SafeRaiseAsync( monitor, e );
            await OnImmediateEventAsync( monitor, job, e );
        }

        internal Task SetFinalResultAsync( IActivityMonitor monitor, CrisJob job, IReadOnlyList<IEvent> events, CrisExecutionHost.ICrisJobResult r )
        {
            return events.Count > 0 ? FinalWithEventsAsync( monitor, job, events, r ) : OnFinalResultAsync( monitor, job, events, r );
        }

        async Task FinalWithEventsAsync( IActivityMonitor monitor, CrisJob job, IReadOnlyList<IEvent> events, CrisExecutionHost.ICrisJobResult r )
        {
            foreach( var e in events )
            {
                Debug.Assert( e != null && (e.CrisPocoModel.Kind != CrisPocoKind.CallerOnlyImmediateEvent && e.CrisPocoModel.Kind != CrisPocoKind.RoutedImmediateEvent) );
                await _hub.AllEventSender.SafeRaiseAsync( monitor, e );
            }
            await OnFinalResultAsync( monitor, job, events, r );
        }

        /// <summary>
        /// Must provide a scoped context into which the commands will be validated and executed.
        /// </summary>
        /// <param name="job">The job that will be executed.</param>
        /// <returns>A scope context.</returns>
        internal protected abstract AsyncServiceScope CreateAsyncScope( CrisJob job );

        /// <summary>
        /// Extension point called when a command has been validated.
        /// This is always called: this signals the start of a command handling.
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
        /// Extension point called when a command emits an immediate event.
        /// Note that all local impacts have been already handled: the <see cref="CrisEventHub"/> has already raised the event.
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="job">The executing job.</param>
        /// <param name="e">The event.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task OnImmediateEventAsync( IActivityMonitor monitor, CrisJob job, IEvent e ) => Task.CompletedTask;

        /// <summary>
        /// Extension point called once a command has been executed.
        /// Note that all local impacts have been already handled: the <see cref="CrisEventHub"/> has already raised all the events.
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="job">The executed job.</param>
        /// <param name="events">Non empty events raised by the command execution.</param>
        /// <param name="r">The final command result.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task OnFinalResultAsync( IActivityMonitor monitor, CrisJob job, IReadOnlyList<IEvent> events, CrisExecutionHost.ICrisJobResult r ) => Task.CompletedTask;

    }

}
