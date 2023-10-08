using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Cris
{
    /// <summary>
    /// Implements <see cref="IExecutedCommand"/> for any <see cref="IAbstractCommand"/>.
    /// </summary>
    public class ExecutedCommand : IExecutedCommand
    {
        IAbstractCommand _command;
        object? _result;
        IReadOnlyList<IEvent> _events;

        /// <summary>
        /// Initializes a new <see cref="ExecutedCommand"/>.
        /// </summary>
        /// <param name="command">The executed command.</param>
        /// <param name="result">The command result (if any).</param>
        /// <param name="events">The emitted events (if any)</param>
        public ExecutedCommand( IAbstractCommand command, object? result, IReadOnlyList<IEvent>? events )
        {
            Throw.CheckNotNullArgument( command );
            _command = command;
            _result = result;
            _events = events ?? Array.Empty<IEvent>();
        }

        /// <inheritdoc />
        public IAbstractCommand Command => _command;

        /// <inheritdoc />
        public object? Result => _result;

        /// <inheritdoc />
        public IReadOnlyList<IEvent> Events => _events;
    }

}
