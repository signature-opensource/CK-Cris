using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace CK.Cris;

/// <summary>
/// Non generic base class for <see cref="ContainerCommandExecutor{T}"/>.
/// A <see cref="CrisExecutionHost"/> relies on an executor to:
/// <list type="bullet">
///   <item>
///   Create a <see cref="AsyncServiceScope"/> to handle command validation and command execution.
///   </item>
///   <item>
///   Expose the execution impacts to the external world thanks to <see cref="OnImmediateEventAsync"/>
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
    /// Configures the ambient services of a job from its reception context (for instance the identity derived from the
    /// party that sent the command). Called with the job's scope, before the incoming validation: when a hub is returned,
    /// the incoming validation and the execution run inline in a scope built from it.
    /// <para>
    /// Returning an error refuses the command (it is not validated nor executed). By default, nothing is configured.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="job">The job.</param>
    /// <param name="services">The job's scoped services.</param>
    /// <returns>An error that refuses the command, or the ambient services to use (null to keep the job's ones).</returns>
    internal protected virtual ValueTask<(ICrisResultError? Error, AmbientServiceHub? Hub)> ConfigureReceptionAsync( IActivityMonitor monitor,
                                                                                                                     CrisJob job,
                                                                                                                     System.IServiceProvider services )
    {
        return ValueTask.FromResult<(ICrisResultError?, AmbientServiceHub?)>( (null, null) );
    }

    /// <summary>
    /// Extension point called when a command emits an immediate event (routed or caller only events).
    /// Note that all local impacts have been already handled: the <see cref="CrisEventHub"/> has already raised the event
    /// and if <see cref="CrisJob.ExecutingCommand"/> is not null, the <see cref="IExecutingCommand.ImmediateEvents"/> have been updated.
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
