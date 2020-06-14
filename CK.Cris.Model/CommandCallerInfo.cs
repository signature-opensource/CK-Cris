using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Captures the primary information that describes the originator of a command.
    /// </summary>
    public sealed class CommandCallerInfo
    {
        /// <summary>
        /// Initializes a new <see cref="CommandCallerInfo"/>.
        /// </summary>
        /// <param name="callerId">The caller identifier.</param>
        /// <param name="correlationId">The optional correlation identifier.</param>
        /// <param name="commandId">The command identifier (if known).</param>
        public CommandCallerInfo( string callerId, string? correlationId, string? commandId = null )
        {
            CallerId = callerId ?? throw new ArgumentNullException( nameof( callerId ) );
            CorrelationId = correlationId;
            CommandId = commandId;
        }

        /// <summary>
        /// Gets the command identifer. It is null until a command identifier is assigned.
        /// See <see cref="SetCommandId(string)"/>
        /// </summary>
        public string? CommandId { get; }

        /// <summary>
        /// Gets the caller identifer. Can be <see cref="String.Empty"/> when no specific exists or is needed.
        /// </summary>
        public string CallerId { get; }

        /// <summary>
        /// Gets the optional correlation identifer.
        /// </summary>
        public string? CorrelationId { get; }

        /// <summary>
        /// Sets a command identifier: must be called only if <see cref="CommandId"/> is currently
        /// null otherwise a <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        /// <param name="commandId">The command identifier: must not be <see cref="String.IsNullOrWhiteSpace(string)"/>.</param>
        /// <returns></returns>
        public CommandCallerInfo SetCommandId( string commandId )
        {
            if( String.IsNullOrWhiteSpace( commandId ) ) throw new ArgumentNullException( nameof( commandId ) );
            if( CommandId != null ) throw new InvalidOperationException( $"CommandId is already assigned to '{CommandId}'. It cannot be changed to {commandId}." );
            return new CommandCallerInfo( CallerId, CorrelationId, commandId );
        }

        /// <summary>
        /// Overridden to display all fields.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"Id: {CommandId}, CallerId: {CallerId}, CorrelationId: {CorrelationId}";

    }
}
