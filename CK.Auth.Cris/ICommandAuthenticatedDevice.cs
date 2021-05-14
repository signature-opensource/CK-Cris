using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Auth
{
    /// <summary>
    /// Extends the basic <see cref="ICommandAuthenticated"/> to add the <see cref="DeviceId"/> field.
    /// </summary>
    [CKTypeDefiner]
    public interface ICommandAuthenticatedDevice : ICommandAuthenticated
    {
        /// <summary>
        /// Gets or sets the device identifier.
        /// The default <see cref="CrisAuthenticationService"/> validates this field against the current <see cref="IAuthenticationInfo.DeviceId"/>.
        /// </summary>
        string? DeviceId { get; set; }
    }
}
