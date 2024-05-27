using CK.Core;
using System.Collections.Immutable;

namespace CK.Cris
{
    /// <summary>
    /// An executed command carries the <see cref="Command"/> (a <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>),
    /// its <see cref="Result"/>, the <see cref="ValidationMessages"/> and can have non empty <see cref="Events"/> on success.
    /// </summary>
    public interface IExecutedCommand
    {
        /// <summary>
        /// Gets the <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>.
        /// </summary>
        IAbstractCommand Command { get; }

        /// <summary>
        /// Gets the validation messages.
        /// </summary>
        ImmutableArray<UserMessage> ValidationMessages { get; }

        /// <summary>
        /// Gets the result of the command. Can be:
        /// <list type="bullet">
        ///  <item>A <see cref="ICrisResultError"/> on validation or execution error.</item>
        ///  <item>A successful null result on success when this command is a <see cref="ICommand"/>.</item>
        ///  <item>A successful result object (that can be null) if this command is a <see cref="ICommand{TResult}"/>.</item>
        /// </list>
        /// </summary>
        object? Result { get; }

        /// <summary>
        /// Gets the non immediate events emitted by the command.
        /// This is non empty only when the command has been successfully executed.
        /// </summary>
        ImmutableArray<IEvent> Events { get; }
    }

}
