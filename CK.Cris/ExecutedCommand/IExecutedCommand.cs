using System.Collections.Generic;

namespace CK.Cris
{
    /// <summary>
    /// An executed command carries the <see cref="Command"/> (a <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>),
    /// its <see cref="Result"/> and can have non empty <see cref="Events"/> on success.
    /// </summary>
    public interface IExecutedCommand
    {
        /// <summary>
        /// Gets the <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>.
        /// </summary>
        IAbstractCommand Command { get; }

        /// <summary>
        /// Gets the result of the command. Can be:
        /// <list type="bullet">
        ///  <item>A <see cref="ICrisResultError"/> on validation or execution error.</item>
        ///  <item>A successful null result on success when this command is a <see cref="ICommand"/>.</item>
        ///  <item>A successful result object if this command is a <see cref="ICommand{TResult}"/>.</item>
        /// </list>
        /// </summary>
        object? Result { get; }

        /// <summary>
        /// Gets the non immediate events emitted by the command.
        /// This is non empty only when the command has been successfully executed.
        /// </summary>
        IReadOnlyList<IEvent> Events { get; }
    }

}
