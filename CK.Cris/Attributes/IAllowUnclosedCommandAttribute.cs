using CK.Core;

namespace CK.Cris;

/// <summary>
/// Attribute marker for parameter attribute that states that a command
/// handler allows its command to not be the "unified", "closed", interface.
/// <para>
/// This interface is duck typed: only its name matter and it can be locally defined
/// in any assemblies that need it.
/// </para>
/// </summary>
public interface IAllowUnclosedCommandAttribute
{
}
