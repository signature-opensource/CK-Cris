using CK.Core;

namespace CK.Auth;

/// <summary>
/// Extends the basic <see cref="ICommandAuthUnsafe"/> to add the <see cref="IAuthImpersonationPart.ActualActorId"/> field.
/// </summary>
[CKTypeDefiner]
public interface ICommandAuthImpersonation : ICommandAuthUnsafe, IAuthImpersonationPart
{
}
