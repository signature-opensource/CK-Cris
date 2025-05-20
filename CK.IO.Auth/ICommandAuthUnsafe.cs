using CK.Cris;

namespace CK.Auth;


/// <summary>
/// This is the most basic authentication related command part: the only guaranty is that the actor identifier is the
/// current <see cref="IAuthenticationInfo.UnsafeUser"/>.
/// </summary>
public interface ICommandAuthUnsafe : ICommandPart, IAuthUnsafePart
{
}
