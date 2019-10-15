using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Immutable encapsulation of a received command.
    /// This is produced by a End Point once the command has been validated, routed to
    /// the <see cref="ICommandReceiver{TCommand}"/> and ultimately handled by <see cref="ICommandHandler{TCommand}"/>
    /// instances.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    public sealed class ReceivedCommand<TCommand> : ReceivedCommand where TCommand : ICommand
    {
        public ReceivedCommand( TCommand command, bool async, string commandId, string callerId, string correlationId )
            : base( async, commandId, callerId, correlationId )
        {
            if( command == null ) throw new ArgumentNullException( nameof( command ) );
            Command = command;
        }

        /// <summary>
        /// The command object itself.
        /// </summary>
        public TCommand Command { get; }

        /// <summary>
        /// Overridden to display all fields.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => base.ToString() + ", Command:" + Command.ToString();
    }
}
