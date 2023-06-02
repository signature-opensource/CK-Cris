using System;
using System.Collections.Generic;

namespace CK.Cris
{
    class ExecutedCommand : IExecutedCommand
    {
        IAbstractCommand _command;
        object? _result;
        IReadOnlyList<IEvent> _events;

        public ExecutedCommand( IAbstractCommand command, object? result, IReadOnlyList<IEvent>? events )
        {
            _command = command;
            _result = result;
            _events = events ?? Array.Empty<IEvent>();
        }

        public IAbstractCommand Command => _command;

        public object? Result => _result;

        public IReadOnlyList<IEvent> Events => _events;
    }

}
