using CK.Core;
using CK.Cris;

namespace CK.Auth;

/// <summary>
/// Keeps the impersonation of a deferred command (a delayed command for instance): the restoration rebuilds the actors
/// from the command, and a command without <see cref="IAuthImpersonationPart"/> carries the impersonated actor only.
/// Without this, an administrator impersonating a user would see the deferred command executed by the user alone.
/// <para>
/// The actual actor cannot be read here (this is synchronous): when the restored authentication is the impersonated actor
/// acting alone, the restoration fails and the command is not executed. A command that must be deferred while impersonating
/// must have the <see cref="IAuthImpersonationPart"/>.
/// </para>
/// </summary>
public sealed class ImpersonationAmbientValuesCapture : IDeferredAmbientValuesCapture
{
    /// <inheritdoc />
    public object? Capture( AmbientServiceHub hub )
    {
        var info = hub.GetCurrentValue<AuthenticationInfo>();
        int actor = info.UnsafeActor.ActorId;
        int actualActor = info.UnsafeActualActor.ActorId;
        return actor != actualActor ? new int[] { actor, actualActor } : null;
    }

    /// <inheritdoc />
    public void Complete( IActivityMonitor monitor, AmbientServiceHub hub, object captured )
    {
        var ids = (int[])captured;
        var restored = hub.GetCurrentValue<AuthenticationInfo>();
        if( restored.UnsafeActor.ActorId == ids[0] && restored.UnsafeActualActor.ActorId != ids[1] )
        {
            Throw.InvalidOperationException( $"The deferred command has been sent by actor #{ids[1]} impersonating actor #{ids[0]} but it has no IAuthImpersonationPart: " +
                                             $"its restored authentication would lose the impersonation. It is not executed." );
        }
    }
}
