using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CK.Cris
{
    /// <summary>
    /// Implements <see cref="IExecutedCommand"/> for any <see cref="IAbstractCommand"/>.
    /// <para>
    /// This is the non-generic <see cref="ExecutedCommand{T}"/>, this cannot be directly instantiated:
    /// only strongly typed <see cref="ExecutedCommand{T}"/> can be instantiated.
    /// </para>
    /// </summary>
    public class ExecutedCommand : IExecutedCommand
    {
        readonly IDeferredCommandExecutionContext? _deferredExecutionInfo;
        readonly IAbstractCommand _command;
        readonly object? _result;
        readonly ImmutableArray<IEvent> _events;
        readonly ImmutableArray<UserMessage> _validationMessages;

        private protected ExecutedCommand( IAbstractCommand command,
                                           object? result,
                                           ImmutableArray<UserMessage> validationMessages,
                                           ImmutableArray<IEvent> events,
                                           IDeferredCommandExecutionContext? deferredExecutionInfo )
        {
            Throw.CheckNotNullArgument( command );
            _command = command;
            _result = result;
            _events = events;
            _deferredExecutionInfo = deferredExecutionInfo;
            _validationMessages = validationMessages;
        }

        /// <inheritdoc />
        public IAbstractCommand Command => _command;

        /// <inheritdoc />
        public ImmutableArray<UserMessage> ValidationMessages => _validationMessages;

        /// <inheritdoc />
        public object? Result => _result;

        /// <inheritdoc />
        public ImmutableArray<IEvent> Events => _events;

        /// <inheritdoc />
        public IDeferredCommandExecutionContext? DeferredExecutionContext => _deferredExecutionInfo;
    }

}
