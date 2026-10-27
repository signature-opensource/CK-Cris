using CK.Core;
using System.Collections.Generic;

namespace CK.Cris;

/// <summary>
/// The plain values captured by the <see cref="IDeferredAmbientValuesCapture"/> services when a command is deferred.
/// </summary>
public sealed class CapturedAmbientValues
{
    readonly (IDeferredAmbientValuesCapture Capture, object Value)[] _values;

    CapturedAmbientValues( (IDeferredAmbientValuesCapture, object)[] values ) => _values = values;

    /// <summary>
    /// Captures the values of the deferring context.
    /// </summary>
    /// <param name="captures">The capture services.</param>
    /// <param name="hub">The ambient services of the deferring context.</param>
    /// <returns>The captured values, null when there is nothing to keep.</returns>
    public static CapturedAmbientValues? Capture( IEnumerable<IDeferredAmbientValuesCapture> captures, AmbientServiceHub hub )
    {
        Throw.CheckNotNullArgument( captures );
        Throw.CheckNotNullArgument( hub );
        List<(IDeferredAmbientValuesCapture, object)>? values = null;
        foreach( var c in captures )
        {
            if( c.Capture( hub ) is { } v ) (values ??= new()).Add( (c, v) );
        }
        return values == null ? null : new CapturedAmbientValues( values.ToArray() );
    }

    /// <summary>
    /// Completes the restored ambient services of the execution.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="hub">The restored ambient services (not locked yet).</param>
    public void Complete( IActivityMonitor monitor, AmbientServiceHub hub )
    {
        foreach( var (c, v) in _values ) c.Complete( monitor, hub, v );
    }
}
