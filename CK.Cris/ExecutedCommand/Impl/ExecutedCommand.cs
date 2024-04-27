using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CK.Cris
{
    /// <summary>
    /// Implements <see cref="IExecutedCommand"/> for any <see cref="IAbstractCommand"/>.
    /// </summary>
    public class ExecutedCommand : IExecutedCommand
    {
        IAbstractCommand _command;
        object? _result;
        ImmutableArray<IEvent> _events;
        ImmutableArray<UserMessage> _validationMessages;

        /// <summary>
        /// Initializes a new <see cref="ExecutedCommand"/>.
        /// </summary>
        /// <param name="command">The executed command.</param>
        /// <param name="result">The command result.</param>
        /// <param name="validationMessages">The validation messages.</param>
        /// <param name="events">The emitted events.</param>
        public ExecutedCommand( IAbstractCommand command, object? result, ImmutableArray<UserMessage> validationMessages, ImmutableArray<IEvent> events )
        {
            Throw.CheckNotNullArgument( command );
            _command = command;
            _result = result;
            _events = events;
            _validationMessages = validationMessages;
        }

        /// <summary>
        /// Initializes a new <see cref="ExecutedCommand"/>.
        /// </summary>
        /// <param name="command">The executed command.</param>
        /// <param name="result">The command result.</param>
        /// <param name="validationMessages">The validation messages.</param>
        /// <param name="events">The emitted events.</param>
        public ExecutedCommand( IAbstractCommand command, object? result, IEnumerable<UserMessage>? validationMessages = null, IEnumerable<IEvent>? events = null )
            : this( command,
                    result,
                    validationMessages != null ? validationMessages.ToImmutableArray() : ImmutableArray<UserMessage>.Empty,
                    events != null ? events.ToImmutableArray() : ImmutableArray<IEvent>.Empty )
        {
        }

        /// <inheritdoc />
        public IAbstractCommand Command => _command;

        /// <inheritdoc />
        public ImmutableArray<UserMessage> ValidationMessages => _validationMessages;

        /// <inheritdoc />
        public object? Result => _result;

        /// <inheritdoc />
        public ImmutableArray<IEvent> Events => _events;
    }

}
