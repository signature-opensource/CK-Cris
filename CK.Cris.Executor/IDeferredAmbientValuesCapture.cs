using CK.Core;

namespace CK.Cris;

/// <summary>
/// Captures, when a command is deferred (a delayed command for instance), plain values of the current ambient services
/// that the restoration of the command (the [RestoreAmbientServices] methods) cannot rebuild, and completes the restored
/// ambient services with them when the command is executed.
/// <para>
/// The captured value must be a plain value (an identifier, a string...), never a service nor an object that holds
/// services: the services of the execution are always the fresh restored ones. A store that persists the deferred
/// commands persists these values: they should be serializable.
/// </para>
/// </summary>
[IsMultiple]
public interface IDeferredAmbientValuesCapture : ISingletonAutoService
{
    /// <summary>
    /// Captures a plain value from the ambient services of the deferring context.
    /// </summary>
    /// <param name="hub">The ambient services of the deferring context.</param>
    /// <returns>The value to keep, null when there is nothing to keep.</returns>
    object? Capture( AmbientServiceHub hub );

    /// <summary>
    /// Completes the restored ambient services of the execution with the captured value.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="hub">The restored ambient services (not locked yet).</param>
    /// <param name="captured">The value returned by <see cref="Capture(AmbientServiceHub)"/>.</param>
    void Complete( IActivityMonitor monitor, AmbientServiceHub hub, object captured );
}
