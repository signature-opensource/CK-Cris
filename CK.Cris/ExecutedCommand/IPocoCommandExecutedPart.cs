using CK.Core;
using System.Collections.Generic;

namespace CK.Cris
{
    /// <summary>
    /// A command part that captures a <see cref="IExecutedCommand"/>.
    /// <see cref="Initialize(IExecutedCommand)"/> can be used to initialize it.
    /// </summary>
    [CKTypeDefiner]
    public interface IPocoCommandExecutedPart : IPoco
    {
        /// <summary>
        /// Gets the command that has been executed.
        /// </summary>
        [NullInvalid]
        IAbstractCommand? Command { get; set; }

        /// <summary>
        /// Gets the validation messages.
        /// </summary>
        IList<UserMessage> ValidationMessages { get; }

        /// <summary>
        /// Gets the result of the command. See <see cref="IExecutedCommand.Result"/>.
        /// </summary>
        object? Result { get; set; }

        /// <summary>
        /// Gets the non immediate events emitted by the command.
        /// This is non empty only when the command has been successfully executed.
        /// </summary>
        IList<IEvent> Events { get; }

        /// <summary>
        /// Initializes this part from a <see cref="IExecutedCommand"/>.
        /// </summary>
        /// <param name="command"></param>
        void Initialize( IExecutedCommand command )
        {
            Command = command.Command;
            ValidationMessages.AddRange( command.ValidationMessages );
            Result = command.Result;
            Events.AddRange( command.Events );
        }
    }
}
