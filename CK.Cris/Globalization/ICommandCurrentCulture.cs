using CK.Core;

namespace CK.Cris;

/// <summary>
/// Command part that specifies the <see cref="CurrentCultureInfo"/> that must be available
/// when validating and handling the command.
/// </summary>
public interface ICommandCurrentCulture : ICommandPart, ICurrentCulturePart
{
}
