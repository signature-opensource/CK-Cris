using CK.Core;

namespace CK.Cris;

/// <summary>
/// Command or Event part that specifies the <see cref="CurrentCultureInfo"/>.
/// </summary>
public interface ICurrentCulturePart : ICrisPocoPart
{
    /// <summary>
    /// Gets or sets the current culture name that must be used when handling
    /// an event or a command. When null, the currently available <see cref="CurrentCultureInfo"/> is
    /// not changed.
    /// </summary>
    string? CurrentCultureName { get; set; }
}
