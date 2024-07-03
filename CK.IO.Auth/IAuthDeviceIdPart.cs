using CK.Core;
using CK.Cris;

namespace CK.Auth
{
    /// <summary>
    /// Extends the basic <see cref="IAuthUnsafePart"/> to add the <see cref="DeviceId"/> field.
    /// </summary>
    [CKTypeDefiner]
    public interface IAuthDeviceIdPart : IAuthUnsafePart
    {
        /// <summary>
        /// Gets or sets the device identifier.
        /// </summary>
        [AmbientServiceValue]
        string? DeviceId { get; set; }
    }
}
