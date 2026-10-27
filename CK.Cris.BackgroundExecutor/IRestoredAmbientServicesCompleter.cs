using CK.Core;

namespace CK.Cris;

/// <summary>
/// Optionally implemented by a <see cref="IDeferredCommandExecutionContext"/> given to
/// <see cref="CrisBackgroundExecutorService.Submit{T}(T, AmbientServiceHub?, ActivityMonitor.Token, IDeferredCommandExecutionContext?, System.Func{IActivityMonitor, IExecutedCommand, System.IServiceProvider?, System.Threading.Tasks.Task}?, bool?)"/>
/// without hub: the ambient services restored from the command (the [RestoreAmbientServices] methods) are completed with
/// what the deferring context kept, before the execution scope is created.
/// <para>
/// A deferring context keeps plain values, never services: the services are always the fresh restored ones.
/// </para>
/// </summary>
public interface IRestoredAmbientServicesCompleter
{
    /// <summary>
    /// Completes the restored ambient services.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="hub">The restored hub (not locked yet).</param>
    void Complete( IActivityMonitor monitor, AmbientServiceHub hub );
}
