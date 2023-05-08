using CK.Core;

namespace CK.Cris
{
    /// <summary>
    /// Defines a command without result. This can be extended with <see cref="ICommand{TResult}"/>.
    /// Any type that extends this interface defines a new command type.
    /// Command type names should keep the initial "I" (of the interface) and
    /// end with "Command".
    /// </summary>
    public interface ICommand : IAbstractCommand
    {
    }

}
