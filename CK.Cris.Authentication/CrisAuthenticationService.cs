using CK.Core;
using CK.Cris;
using CK.Cris.AmbientValues;
using System;
using System.Threading.Tasks;

namespace CK.Auth;

/// <summary>
/// Authentication service that handles the <see cref="IAuthUnsafePart"/>, <see cref="IAuthNormalPart"/>, <see cref="IAuthCriticalPart"/>,
/// <see cref="IAuthDeviceIdPart"/> and <see cref="IAuthImpersonationPart"/> with the <see cref="AuthenticationInfo"/> ambient service.
/// <para>
/// This type has on purpose the same full name as the legacy CK.Cris.Auth service (that works with CK.Auth.Abstractions):
/// both packages cannot be installed together.
/// </para>
/// <para>
/// The levels are the ones of the actors (<see cref="ActorInfo.Level"/>). The ambient values and the validations use
/// the <see cref="AuthenticationInfo.UnsafeActor"/> and <see cref="AuthenticationInfo.UnsafeActualActor"/> so that a
/// session at <see cref="AuthLevel.Unsafe"/> level (a weak actor for instance) can send <see cref="IAuthUnsafePart"/> commands.
/// </para>
/// </summary>
/// <remarks>
/// This currently registers <see cref="IAuthAmbientValues"/> (<c>ActorId</c>, <c>ActualActorId</c> and <c>DeviceId</c>) but this is temporary:
/// IAmbientValues initialization will soon be handled by Default Implementation Methods.
/// </remarks>
public class CrisAuthenticationService : ISingletonAutoService
{
    readonly IActorInfoProvider _actorInfoProvider;

    /// <summary>
    /// Initializes a new <see cref="CrisAuthenticationService"/>.
    /// </summary>
    /// <param name="actorInfoProvider">The actor info provider used to restore the authentication.</param>
    public CrisAuthenticationService( IActorInfoProvider actorInfoProvider )
    {
        Throw.CheckNotNullArgument( actorInfoProvider );
        _actorInfoProvider = actorInfoProvider;
    }

    /// <summary>
    /// Fills the <see cref="IAuthAmbientValues"/> from the current authentication.
    /// </summary>
    /// <param name="cmd">The ambient values collector command.</param>
    /// <param name="authInfo">The current authentication.</param>
    /// <param name="values">The result to fill.</param>
    [CommandPostHandler]
    public virtual void GetAuthenticationValues( IAmbientValuesCollectCommand cmd, AuthenticationInfo authInfo, IAuthAmbientValues values )
    {
        values.ActorId = authInfo.UnsafeActor.ActorId;
        values.ActualActorId = authInfo.UnsafeActualActor.ActorId;
        values.DeviceId = authInfo.DeviceId;
    }

    /// <summary>
    /// Checks the <see cref="IAuthUnsafePart.ActorId"/> against the current <see cref="AuthenticationInfo.UnsafeActor"/>
    /// and its level when the part is a <see cref="IAuthNormalPart"/> or a <see cref="IAuthCriticalPart"/>.
    /// </summary>
    /// <param name="c">The message collector.</param>
    /// <param name="crisPoco">The command or event to validate.</param>
    /// <param name="info">The current authentication.</param>
    [IncomingValidator]
    public virtual void ValidateAuthenticatedPart( UserMessageCollector c, IAuthUnsafePart crisPoco, AuthenticationInfo info )
    {
        if( !crisPoco.ActorId.HasValue )
        {
            c.Error( "Invalid property: ActorId cannot be null." );
        }
        else if( crisPoco.ActorId.Value != info.UnsafeActor.ActorId )
        {
            c.Error( "Invalid actor identifier: the provided identifier doesn't match the current authentication." );
        }
        else if( crisPoco is IAuthCriticalPart )
        {
            if( info.UnsafeActor.Level != AuthLevel.Critical )
            {
                c.Error( "Invalid authentication level: Critical authentication level required." );
            }
        }
        else if( crisPoco is IAuthNormalPart )
        {
            if( info.UnsafeActor.Level < AuthLevel.Normal )
            {
                c.Error( "Invalid authentication level: Normal authentication level required." );
            }
        }
    }

    /// <summary>
    /// Checks the <see cref="IAuthDeviceIdPart.DeviceId"/> against the current <see cref="AuthenticationInfo.DeviceId"/>.
    /// </summary>
    /// <param name="c">The message collector.</param>
    /// <param name="crisPoco">The command or event to validate.</param>
    /// <param name="info">The current authentication.</param>
    [IncomingValidator]
    public virtual void ValidateDevicePart( UserMessageCollector c, IAuthDeviceIdPart crisPoco, AuthenticationInfo info )
    {
        if( crisPoco.DeviceId == null )
        {
            c.Error( "Invalid property: DeviceId cannot be null." );
        }
        else if( crisPoco.DeviceId != info.DeviceId )
        {
            c.Error( "Invalid device identifier: the command provided identifier doesn't match the current authentication." );
        }
    }

    /// <summary>
    /// Checks the <see cref="IAuthImpersonationPart.ActualActorId"/> against the current <see cref="AuthenticationInfo.UnsafeActualActor"/>.
    /// </summary>
    /// <param name="c">The message collector.</param>
    /// <param name="cmd">The command or event to validate.</param>
    /// <param name="info">The current authentication.</param>
    [IncomingValidator]
    public virtual void ValidateImpersonationPart( UserMessageCollector c, IAuthImpersonationPart cmd, AuthenticationInfo info )
    {
        if( !cmd.ActualActorId.HasValue )
        {
            c.Error( "Invalid property: ActualActorId cannot be null." );
        }
        else if( cmd.ActualActorId.Value != info.UnsafeActualActor.ActorId )
        {
            c.Error( "Invalid actual actor identifier: the provided identifier doesn't match the current authentication." );
        }
    }

    /// <summary>
    /// Restores the <see cref="AuthenticationInfo"/> from a command or event that has been validated when it entered
    /// the System: its identifiers are trustable.
    /// <para>
    /// The actors are read by the <see cref="IActorInfoProvider"/> (their current state is used) and the level is the one
    /// required by the parts (see <see cref="AuthenticationInfo.CreateRestored(ActorInfo, ActorInfo, AuthLevel, string?, DateTime)"/>).
    /// When an actor cannot be found, the restoration fails: the command is never executed as the anonymous.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="crisPoco">The command or event to execute.</param>
    /// <param name="hub">The ambient services hub to configure.</param>
    /// <returns>The awaitable.</returns>
    [RestoreAmbientServices]
    public virtual async ValueTask RestoreAsync( IActivityMonitor monitor, IAuthUnsafePart crisPoco, AmbientServiceHub hub )
    {
        Throw.CheckState( "ActorId must not be null.", crisPoco.ActorId is not null );
        int actorId = crisPoco.ActorId.Value;
        int actualActorId = actorId;
        if( crisPoco is IAuthImpersonationPart imp )
        {
            Throw.CheckState( "ActualActorId must not be null.", imp.ActualActorId is not null );
            actualActorId = imp.ActualActorId.Value;
        }
        var deviceId = crisPoco is IAuthDeviceIdPart d ? d.DeviceId : null;
        AuthenticationInfo info;
        if( actualActorId == 0 )
        {
            Throw.CheckState( "Only the anonymous can be impersonated by the anonymous.", actorId == 0 );
            info = AuthenticationInfo.CreateRestored( ActorInfo.Anonymous, ActorInfo.Anonymous, AuthLevel.None, deviceId, DateTime.UtcNow );
        }
        else
        {
            var actualActor = await ReadActorAsync( monitor, actualActorId ).ConfigureAwait( false );
            var actor = actorId == actualActorId
                        ? actualActor
                        : actorId == 0
                            ? ActorInfo.Anonymous
                            : await ReadActorAsync( monitor, actorId ).ConfigureAwait( false );
            var level = crisPoco is IAuthCriticalPart
                            ? AuthLevel.Critical
                            : crisPoco is IAuthNormalPart
                                ? AuthLevel.Normal
                                : AuthLevel.Unsafe;
            info = AuthenticationInfo.CreateRestored( actualActor, actor, level, deviceId, DateTime.UtcNow );
        }
        hub.Override( info );
    }

    async ValueTask<ActorInfo> ReadActorAsync( IActivityMonitor monitor, int actorId )
    {
        var u = await _actorInfoProvider.GetActorInfoAsync( monitor, actorId ).ConfigureAwait( false );
        if( u == null )
        {
            Throw.InvalidOperationException( $"Unable to restore the authentication: actor #{actorId} cannot be found or used." );
        }
        return u;
    }
}
