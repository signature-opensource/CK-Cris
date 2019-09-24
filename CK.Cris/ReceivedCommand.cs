using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Encapsulates the received command.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    public readonly struct ReceivedCommand<TCommand> where TCommand : ICommand
    {
        /// <summary>
        /// The command object itself.
        /// </summary>
        public readonly TCommand Command;

        /// <summary>
        /// True if the command is handled asynchronously.
        /// </summary>
        public readonly bool AsynchronousHandlingMode;

        /// <summary>
        /// The command identifier that is assigned by the End Point.
        /// </summary>
        public readonly string CommandId;

        /// <summary>
        /// The caller identifier. 
        /// </summary>
        public readonly string CallerId;

        /// <summary>
        /// The optional correlation identifier.
        /// </summary>
        public readonly string CorrelationId;
    }
}
