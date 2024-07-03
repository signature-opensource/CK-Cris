using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Auth
{

    /// <summary>
    /// Extends the basic <see cref="ICommandAuthUnsafe"/> to add the <see cref="IAuthDeviceIdPart.DeviceId"/> field.
    /// </summary>
    [CKTypeDefiner]
    public interface ICommandAuthDeviceId : ICommandAuthUnsafe, IAuthDeviceIdPart
    {
    }
}
