using CK.Core;

namespace CK.Cris;


/// <summary>
/// Describes a type of command that expects a result.
/// Any type that extends this interface defines a new command type.
/// <para>
/// Command type names should keep the initial "I" (of the interface) and
/// end with "Command".
/// </para>
/// </summary>
/// <typeparam name="TResult">Type of the expected result.</typeparam>
public interface ICommand<out TResult> : IAbstractCommand
{
    /// <summary>
    /// Required by the CKomposale framwork.
    /// </summary>
    [AutoImplementationClaim] public static TResult TResultType => default!;
}
