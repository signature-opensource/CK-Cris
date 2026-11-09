using CK.Core;
using CK.Cris;

namespace CK.Auth;

/// <summary>
/// Keeps the <see cref="AuthenticationInfo.CallerPartyFullName"/> of a deferred command (a delayed command for instance): the
/// restoration rebuilds the actors from the command, never the caller party, so the restored authentication is
/// completed with the captured full name.
/// <para>
/// The restored levels are the ones the command declares (they have been checked against the possibly capped identity
/// when the command has been received): the caller party is not used to cap them again, it is kept so that the
/// validators and handlers of the execution see who called.
/// </para>
/// </summary>
public sealed class CallerPartyAmbientValuesCapture : IDeferredAmbientValuesCapture
{
    /// <inheritdoc />
    public object? Capture( AmbientServiceHub hub )
    {
        return hub.GetCurrentValue<AuthenticationInfo>().CallerPartyFullName;
    }

    /// <inheritdoc />
    public void Complete( IActivityMonitor monitor, AmbientServiceHub hub, object captured )
    {
        hub.Override( hub.GetCurrentValue<AuthenticationInfo>().WithCallerParty( (string)captured ) );
    }
}
