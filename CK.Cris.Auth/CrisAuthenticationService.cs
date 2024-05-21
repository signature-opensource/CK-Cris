using CK.Core;
using CK.Cris;
using CK.Cris.AmbientValues;

namespace CK.Auth
{
    /// <summary>
    /// Authentication service registers <see cref="IAuthAmbientValues"/> (<c>ActorId</c>, <c>ActualActorId</c> and <c>DeviceId</c>)
    /// and validates the <see cref="ICommandAuthUnsafe"/>, <see cref="ICommandAuthNormal"/>, <see cref="ICommandAuthCritical"/>, <see cref="ICommandAuthDeviceId"/>
    /// and <see cref="ICommandAuthImpersonation"/>.
    /// </summary>
    /// <remarks>
    /// This default implementation may be specialized if needed.
    /// </remarks>
    public class CrisAuthenticationService : IAutoService
    {
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
        /// Checks whether <see cref="IAuthNormalPart.ActorId"/> is the same as the current <see cref="IAuthenticationInfo.User"/>
        /// identifier and if not, emits an error in the message collector.
        /// <para>
        /// If the command is marked with <see cref="ICommandAuthCritical"/>, the <see cref="IAuthenticationInfo.Level"/> must be
        /// critical otherwise an error is emitted.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Note that the command's ActorId must exactly match the current <see cref="IAuthenticationInfo.User"/>. 
        /// </remarks>
        /// <param name="c">The message collector.</param>
        /// <param name="cmd">The command to validate.</param>
        /// <param name="info">The current authentication information.</param>
        [CommandIncomingValidator]
        public virtual void ValidateAuthenticatedPart( UserMessageCollector c, ICommandAuthUnsafe cmd, IAuthenticationInfo info )
        {
            // Temporary:
            // - This will be handled by Poco validation.
            // - The [AmbientServiceValue] is a INullInvalidAttribute, null will be rejected.
            if( !cmd.ActorId.HasValue )
            {
                c.Error( $"Invalid property: ActorId cannot be null." );
            }
            else if( cmd.ActorId != info.UnsafeUser.UserId )
            {
                c.Error( "Invalid actor identifier: the command provided identifier doesn't match the current authentication." );
            }
            else if( cmd is ICommandAuthCritical )
            {
                if( info.Level != AuthLevel.Critical )
                {
                    c.Error( "Invalid authentication level: the command requires a Critical level." );
                }
            }
            else if( cmd is ICommandAuthNormal )
            {
                if( info.Level < AuthLevel.Normal )
                {
                    c.Error( "Invalid authentication level: the command requires a Normal level." );
                }
            }
        }

        /// <summary>
        /// Checks whether <see cref="ICommandAuthDeviceId.DeviceId"/> is the same as the current <see cref="IAuthenticationInfo.DeviceId"/>
        /// and if not, emits an error in the message collector.
        /// </summary>
        /// <param name="c">The message collector.</param>
        /// <param name="cmd">The command to validate.</param>
        /// <param name="info">The current authentication information.</param>
        [CommandIncomingValidator]
        public virtual void ValidateDevicePart( UserMessageCollector c, ICommandAuthDeviceId cmd, IAuthenticationInfo info )
        {
            // Temporary:
            // - This will be handled by Poco validation.
            // - The [AmbientServiceValue] is a INullInvalidAttribute, null will be rejected.
            if( cmd.DeviceId == null )
            {
                c.Error( $"Invalid property: {nameof( ICommandAuthDeviceId.DeviceId )} cannot be null." );
            }
            else if( cmd.DeviceId != info.DeviceId )
            {
                c.Error( "Invalid device identifier: the command provided identifier doesn't match the current authentication." );
            }
        }

        /// <summary>
        /// Checks whether <see cref="ICommandAuthImpersonation.ActualActorId"/> is the same as the current <see cref="IAuthenticationInfo.ActualUser"/>
        /// identifier and if not, emits an error in the message collector.
        /// </summary>
        /// <param name="c">The message collector.</param>
        /// <param name="cmd">The command to validate.</param>
        /// <param name="info">The current authentication information.</param>
        [CommandIncomingValidator]
        public virtual void ValidateImpersonationPart( UserMessageCollector c, ICommandAuthImpersonation cmd, IAuthenticationInfo info )
        {
            // Temporary:
            // - This will be handled by Poco validation.
            // - The [AmbientServiceValue] is a INullInvalidAttribute, null will be rejected.
            if( !cmd.ActorId.HasValue )
            {
                c.Error( $"Invalid property: {nameof( ICommandAuthImpersonation.ActualActorId )} cannot be null." );
            }
            else if( cmd.ActualActorId != info.ActualUser.UserId )
            {
                c.Error( "Invalid actual actor identifier: the command provided identifier doesn't match the current authentication." );
            }
        }
    }
}
