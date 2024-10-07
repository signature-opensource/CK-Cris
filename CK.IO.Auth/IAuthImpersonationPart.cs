using CK.Core;
using CK.Cris;

namespace CK.Auth;

/// <summary>
/// Extends the basic <see cref="IAuthUnsafePart"/> to add the <see cref="ActualActorId"/> field.
/// </summary>
[CKTypeDefiner]
public interface IAuthImpersonationPart : IAuthUnsafePart
{
    /// <summary>
    /// Gets or sets the actual actor identifier: the one that is connected, regardless of any impersonation.
    /// </summary>
    [AmbientServiceValue]
    int? ActualActorId { get; set; }
}
