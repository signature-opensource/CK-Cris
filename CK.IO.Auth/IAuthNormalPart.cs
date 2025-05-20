using CK.Core;

namespace CK.Auth;

/// <summary>
/// Extends <see cref="IAuthUnsafePart"/> to ensure that the authentication level is <see cref="AuthLevel.Normal"/>
/// (or <see cref="AuthLevel.Critical"/>).
/// </summary>
[CKTypeDefiner]
public interface IAuthNormalPart : IAuthUnsafePart
{
}
