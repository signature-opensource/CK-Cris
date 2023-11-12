namespace CK.Cris
{
    /// <summary>
    /// Defines a command without result.
    /// Any type that extends this interface defines a new command type.
    /// <para>
    /// Command type names should keep the initial "I" (of the interface) and
    /// end with "Command".
    /// </para>
    /// </summary>
    public interface ICommand : IAbstractCommand
    {
    }

}
