using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Auth
{
    /// <summary>
    /// Extends the basic <see cref="ICommandAuthUnsafe"/> to add the <see cref="DeviceId"/> field.
    /// </summary>
    [CKTypeDefiner]
    public interface ICommandAuthDeviceId : ICommandAuthUnsafe
    {
        /// <summary>
        /// Gets or sets the device identifier.
        /// The default <see cref="CrisAuthenticationService"/> validates this field against the
        /// current <see cref="IAuthenticationInfo.DeviceId"/>.
        /// </summary>
        [EndpointValue]
        string? DeviceId { get; set; }
    }
}
