using CK.Cris;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Auth
{
    /// <summary>
    /// Defines the authentication properties that are considered as ambient values: command
    /// properties with these names are automatically configured.
    /// </summary>
    public interface IAuthEndpointValues : CK.Cris.EndpointValues.IEndpointValues
    {
        /// <summary>
        /// Gets or sets the <see cref="IAuthenticationInfo.User"/> identifier.
        /// </summary>
        int ActorId { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IAuthenticationInfo.ActualUser"/> identifier.
        /// </summary>
        int ActualActorId { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IAuthenticationInfo.DeviceId"/> identifier.
        /// </summary>
        string DeviceId { get; set; }
    }
}
