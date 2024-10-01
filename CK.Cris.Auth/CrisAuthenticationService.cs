using CK.Core;
using CK.Cris;
using CK.Cris.AmbientValues;
using CK.Setup;
using System;
using System.Threading.Tasks;

namespace CK.Auth
{
    /// <summary>
    /// Authentication service handles the <see cref="IAuthUnsafePart"/>, <see cref="IAuthNormalPart"/>, <see cref="IAuthCriticalPart"/>, <see cref="IAuthDeviceIdPart"/>
    /// and <see cref="IAuthImpersonationPart"/>.
    /// <para>
    /// These parts have [IncomingValidator] and [RestoreAmbientServices] handlers.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This default implementation may be specialized if needed.
    /// <para>
    /// This currently registers <see cref="IAuthAmbientValues"/> (<c>ActorId</c>, <c>ActualActorId</c> and <c>DeviceId</c>) but this is temporary:
    /// IAmbientValues initialization will soon be handled by Default Implementation Methods.
    /// </para>
    /// </remarks>
    public class CrisAuthenticationService : ISingletonAutoService
    {
        readonly IAuthenticationTypeSystem _authenticationTypeSystem;
        readonly IUserInfoProvider _userInfoProvider;

        /// <summary>
        /// Initializes a new <see cref="CrisAuthenticationService"/>.
        /// </summary>
        /// <param name="authenticationTypeSystem">The type system.</param>
        /// <param name="userInfoProvider">The user info provider.</param>
        public CrisAuthenticationService( IAuthenticationTypeSystem authenticationTypeSystem, IUserInfoProvider userInfoProvider )
        {
            Throw.CheckNotNullArgument( authenticationTypeSystem );
            Throw.CheckNotNullArgument( userInfoProvider );
            _authenticationTypeSystem = authenticationTypeSystem;
            _userInfoProvider = userInfoProvider;
        }

        /// <summary>
        /// Fills the <see cref="IAuthAmbientValues"/> from the current <paramref name="authInfo"/>.
        /// </summary>
        /// <param name="cmd">The ubiquitous values collector command.</param>
        /// <param name="authInfo">The current authentication information.</param>
        /// <param name="values">The result collector.</param>
        [CommandPostHandler]
        public virtual void GetAuthenticationValues( IAmbientValuesCollectCommand cmd, IAuthenticationInfo authInfo, IAuthAmbientValues values )
        {
            values.ActorId = authInfo.User.UserId;
            values.ActualActorId = authInfo.ActualUser.UserId;
            values.DeviceId = authInfo.DeviceId;
        }

        /// <summary>
        /// Checks whether <see cref="IAuthUnsafePart.ActorId"/> is the same as the current <see cref="IAuthenticationInfo.User"/>
        /// identifier and if not, emits an error in the message collector.
        /// <para>
        /// If the Poco is marked with <see cref="IAuthCriticalPart"/> (or <see cref="IAuthNormalPart"/>), the <see cref="IAuthenticationInfo.Level"/> must be
        /// <see cref="AuthLevel.Critical"/> (resp. <see cref="AuthLevel.Normal"/>) otherwise an error is emitted.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Note that the Poco's ActorId must exactly match the current <see cref="IAuthenticationInfo.UnsafeUser"/>. 
        /// </remarks>
        /// <param name="c">The message collector.</param>
        /// <param name="crisPoco">The command or event to validate.</param>
        /// <param name="info">The current authentication information.</param>
        [IncomingValidator]
        public virtual void ValidateAuthenticatedPart( UserMessageCollector c, IAuthUnsafePart crisPoco, IAuthenticationInfo info )
        {
            // Temporary:
            // - This will be handled by Poco validation.
            // - The [AmbientServiceValue] is a INullInvalidAttribute, null will be rejected.
            if( !crisPoco.ActorId.HasValue )
            {
                c.Error( $"Invalid property: ActorId cannot be null." );
            }
            else if( crisPoco.ActorId != info.UnsafeUser.UserId )
            {
                c.Error( "Invalid actor identifier: the provided identifier doesn't match the current authentication." );
            }
            else if( crisPoco is IAuthCriticalPart )
            {
                if( info.Level != AuthLevel.Critical )
                {
                    c.Error( "Invalid authentication level: Critical authentication level required." );
                }
            }
            else if( crisPoco is IAuthNormalPart )
            {
                if( info.Level < AuthLevel.Normal )
                {
                    c.Error( "Invalid authentication level: Normal authentication level required." );
                }
            }
        }

        /// <summary>
        /// Checks whether <see cref="IAuthDeviceIdPart.DeviceId"/> is the same as the current <see cref="IAuthenticationInfo.DeviceId"/>
        /// and if not, emits an error in the message collector.
        /// </summary>
        /// <param name="c">The message collector.</param>
        /// <param name="crisPoco">The command or event to validate.</param>
        /// <param name="info">The current authentication information.</param>
        [IncomingValidator]
        public virtual void ValidateDevicePart( UserMessageCollector c, IAuthDeviceIdPart crisPoco, IAuthenticationInfo info )
        {
            // Temporary:
            // - This will be handled by Poco validation.
            // - The [AmbientServiceValue] is a INullInvalidAttribute, null will be rejected.
            if( crisPoco.DeviceId == null )
            {
                c.Error( $"Invalid property: DeviceId cannot be null." );
            }
            else if( crisPoco.DeviceId != info.DeviceId )
            {
                c.Error( "Invalid device identifier: the command provided identifier doesn't match the current authentication." );
            }
        }

        /// <summary>
        /// Checks whether <see cref="IAuthImpersonationPart.ActualActorId"/> is the same as the current <see cref="IAuthenticationInfo.ActualUser"/>
        /// identifier and if not, emits an error in the message collector.
        /// </summary>
        /// <param name="c">The message collector.</param>
        /// <param name="cmd">The command or event to validate.</param>
        /// <param name="info">The current authentication information.</param>
        [IncomingValidator]
        public virtual void ValidateImpersonationPart( UserMessageCollector c, IAuthImpersonationPart cmd, IAuthenticationInfo info )
        {
            // Temporary:
            // - This will be handled by Poco validation.
            // - The [AmbientServiceValue] is a INullInvalidAttribute, null will be rejected.
            if( !cmd.ActualActorId.HasValue )
            {
                c.Error( $"Invalid property: ActualActorId cannot be null." );
            }
            else if( cmd.ActualActorId != info.ActualUser.UserId )
            {
                c.Error( "Invalid actual actor identifier: the provided identifier doesn't match the current authentication." );
            }
        }

        /// <summary>
        /// Creates a <see cref="IAuthenticationInfo"/> from the different authentication parts that reflects them.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="crisPoco">The command or event.</param>
        /// <param name="hub">The ambient services hub.</param>
        /// <returns>The awaitable.</returns>
        [RestoreAmbientServices]
        public virtual async ValueTask RestoreAsync( IActivityMonitor monitor, IAuthUnsafePart crisPoco, AmbientServiceHub hub )
        {
            Throw.CheckState( crisPoco.ActorId is not null );
            IUserInfo user = await _userInfoProvider.GetUserInfoAsync( monitor, crisPoco.ActorId.Value ).ConfigureAwait( false );
            IUserInfo? actualUser = user;
            if( crisPoco is IAuthImpersonationPart imp )
            {
                Throw.CheckState( imp.ActualActorId is not null );
                if( imp.ActualActorId.Value != user.UserId )
                {
                    actualUser = await _userInfoProvider.GetUserInfoAsync( monitor, crisPoco.ActorId.Value ).ConfigureAwait( false );
                }
            }
            DateTime? expires = null;
            DateTime? criticalExpires = null;
            if( crisPoco is IAuthNormalPart )
            {
                expires = DateTime.UtcNow.AddDays( 30 );
                if( crisPoco is IAuthCriticalPart )
                {
                    criticalExpires = expires;
                }
            }
            var deviceId = crisPoco is IAuthDeviceIdPart d ? d.DeviceId : null;

            var authInfo = _authenticationTypeSystem.AuthenticationInfo.Create( actualUser, expires, criticalExpires, deviceId );
            if( user != actualUser )
            {
                authInfo =  authInfo.Impersonate( user );
            }
            Throw.DebugAssert( authInfo.Level == (crisPoco is IAuthCriticalPart
                                                    ? AuthLevel.Critical
                                                    : crisPoco is IAuthNormalPart
                                                        ? AuthLevel.Normal
                                                        : AuthLevel.None) );
            hub.Override( authInfo );
        }
    }
}
