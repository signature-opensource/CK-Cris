using CK.Core;
using CK.Cris;
using CK.Cris.AmbientValues;
using System.Threading.Tasks;
using System;

namespace CK.Auth;

/// <summary>
/// Defines the authentication properties that are ambient values (coming
/// from <see cref="IAuthenticationInfo"/> ambient service): command properties
/// with these names are automatically configured.
/// </summary>
public interface IAuthAmbientValues : IAmbientValues
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
