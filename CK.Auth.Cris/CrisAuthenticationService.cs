using CK.Core;
using CK.Cris;
using CK.Cris.AmbientValues;
using System;
using System.Collections.Generic;

namespace CK.Auth
{
    /// <summary>
    /// Authentication service registers <c>ActorId</c>, <c>ActualActorId</c> and <c>DeviceId</c> into <see cref="IAmbientValuesCollectCommand"/>
    /// based on the <see cref="IAuthenticationInfo"/> and validates the <see cref="ICommandAuthenticated"/>, <see cref="ICommandAuthenticatedDevice"/>
    /// and <see cref="ICommandAuthenticatedImpersonation"/>.
    /// </summary>
    /// <remarks>
    /// This default implementation may be specialized if needed.
    /// </remarks>
    public class CrisAuthenticationService : IAutoService
    {
        /// <summary>
        /// Fills the <see cref="IAmbientValuesCollectCommand"/> result with the <c>ActorId</c>, <c>ActualActorId</c> and <c>DeviceId</c>
        /// values from the current <paramref name="authInfo"/>.
        /// </summary>
        /// <param name="cmd">The ambient values collector command .</param>
        /// <param name="authInfo">The current authentication information.</param>
        /// <param name="values">The result collector.</param>
        [CommandPostHandler]
        public virtual void GetAmbientValues( IAmbientValuesCollectCommand cmd, IAuthenticationInfo authInfo, Dictionary<string, object?> values )
        {
            values.Add( nameof( ICommandAuthenticated.ActorId ), authInfo.User.UserId );
            values.Add( nameof( ICommandAuthenticatedImpersonation.ActualActorId ), authInfo.ActualUser.UserId );
            values.Add( nameof( ICommandAuthenticatedDevice.DeviceId ), authInfo.DeviceId );
        }

        /// <summary>
        /// Checks whether <see cref="ICommandAuthenticated.ActorId"/> is the same as the current <see cref="IAuthenticationInfo.User"/>
        /// identifier and if not, emits an error in the <paramref name="monitor"/>.
        /// <para>
        /// If the command is marked with <see cref="ICommandAuthenticatedCritical"/>, the <see cref="IAuthenticationInfo.Level"/> must be
        /// critical otherwise an error is emitted.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Note that the command's ActorId must exactly match the current <see cref="IAuthenticationInfo.User"/>. 
        /// </remarks>
        /// <param name="monitor">The monitor to use to raise errors or warnings.</param>
        /// <param name="cmd">The command to validate.</param>
        /// <param name="info">The current authentication information.</param>
        [CommandValidator]
        public virtual void ValidateAuthenticatedPart( IActivityMonitor monitor, ICommandAuthenticated cmd, IAuthenticationInfo info )
        {
            if( cmd.ActorId != info.User.UserId )
            {
                monitor.Error( "Invalid actor identifier: the command provided identifier doesn't match the current authentication." );
            }
            else if( cmd is ICommandAuthenticatedCritical )
            {
                if( info.Level != AuthLevel.Critical )
                {
                    monitor.Error( "Invalid authentication level: the command requires a Critical level." );
                }
            }
        }

        /// <summary>
        /// Checks whether <see cref="ICommandAuthenticatedDevice.DeviceId"/> is the same as the current <see cref="IAuthenticationInfo.DeviceId"/>
        /// and if not, emits an error in the <paramref name="monitor"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use to raise errors or warnings.</param>
        /// <param name="cmd">The command to validate.</param>
        /// <param name="info">The current authentication information.</param>
        [CommandValidator]
        public virtual void ValidateDevicePart( IActivityMonitor monitor, ICommandAuthenticatedDevice cmd, IAuthenticationInfo info )
        {
            if( cmd.DeviceId != info.DeviceId )
            {
                monitor.Error( "Invalid device identifier: the command provided identifier doesn't match the current authentication." );
            }
        }

        /// <summary>
        /// Checks whether <see cref="ICommandAuthenticatedImpersonation.ActualActorId"/> is the same as the current <see cref="IAuthenticationInfo.ActualUser"/>
        /// identifier and if not, emits an error in the <paramref name="monitor"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use to raise errors or warnings.</param>
        /// <param name="cmd">The command to validate.</param>
        /// <param name="info">The current authentication information.</param>
        [CommandValidator]
        public virtual void ValidateImpersonationPart( IActivityMonitor monitor, ICommandAuthenticatedImpersonation cmd, IAuthenticationInfo info )
        {
            if( cmd.ActualActorId != info.ActualUser.UserId )
            {
                monitor.Error( "Invalid actual actor identifier: the command provided identifier doesn't match the current authentication." );
            }
        }
    }
}
