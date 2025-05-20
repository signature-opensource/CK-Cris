using CK.Core;

namespace CK.Cris;

/// <summary>
/// This abstract interface marker is a simple <see cref="IPoco"/> that tags
/// all the objects managed by Cris. This super definer is not intended to be used
/// directly: <see cref="IEvent"/>, <see cref="ICommand"/> and <see cref="ICommand{TResult}"/>
/// are the interfaces to use to define events, commands without result and commands with a result.
/// </summary>
[CKTypeSuperDefiner]
public interface ICrisPoco : IPoco
{
    /// <summary>
    /// Gets the <see cref="ICrisPocoModel"/> that describes this Cris poco.
    /// This property is automatically implemented. 
    /// </summary>
    [AutoImplementationClaim]
    ICrisPocoModel CrisPocoModel { get; }
}
