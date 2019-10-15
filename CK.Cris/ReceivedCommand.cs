using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Immutable encapsulation of a received command data: this is the base (non generic) type of <see cref="ReceivedCommand{TCommand}"/>.
    /// </summary>
    public class ReceivedCommand
    {
        private protected ReceivedCommand( bool async, string commandId, string callerId, string correlationId )
        {
            if( commandId == null ) throw new ArgumentNullException( nameof( commandId ) );
            if( callerId == null ) throw new ArgumentNullException( nameof( callerId ) );
            if( correlationId == null ) throw new ArgumentNullException( nameof( correlationId ) );
            AsynchronousHandlingMode = async;
            CommandId = commandId;
            CallerId = callerId;
            CorrelationId = correlationId;
        }

        /// <summary>
        /// True if the command is handled asynchronously.
        /// </summary>
        public bool AsynchronousHandlingMode { get; }

        /// <summary>
        /// The command identifier that is assigned by the End Point.
        /// </summary>
        public string CommandId { get; }

        /// <summary>
        /// The caller identifier. 
        /// </summary>
        public string CallerId { get; }

        /// <summary>
        /// The optional correlation identifier.
        /// </summary>
        public string CorrelationId { get; }

        /// <summary>
        /// Overridden to display all fields.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"Id: {CommandId}, CallerId: {CallerId}, CorrelationId: {CorrelationId}, Async: {AsynchronousHandlingMode}";
    }
}
