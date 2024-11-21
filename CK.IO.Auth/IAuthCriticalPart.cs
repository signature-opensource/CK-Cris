using CK.Core;

namespace CK.Auth;

/// <summary>
/// Extends <see cref="ICommandAuthNormal"/> to ensure that the authentication level is <see cref="AuthLevel.Critical"/>.
/// </summary>
[CKTypeDefiner]
public interface IAuthCriticalPart : IAuthNormalPart
{
}
