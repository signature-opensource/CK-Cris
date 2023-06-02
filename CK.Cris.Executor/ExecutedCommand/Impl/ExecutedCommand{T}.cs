using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CK.Cris
{
    sealed class ExecutedCommand<T> : ExecutedCommand, IExecutedCommand<T> where T : class, IAbstractCommand
    {
        public ExecutedCommand( T command, object? result, IReadOnlyList<IEvent>? events )
            : base( command, result, events )
        {
        }

        public new T Command => Unsafe.As<T>( base.Command );


        sealed class ResultAdapter<TResult> : IExecutedCommand<T>.WithResult<TResult>
        {
            readonly ExecutedCommand<T> _command;

            public ResultAdapter( ExecutedCommand<T> command )
            {
                _command = command;
            }

            public new TResult Result => (TResult)_command.Result!;

            public T Command => _command.Command;

            public IReadOnlyList<IEvent> Events => _command.Events;

            IAbstractCommand IExecutedCommand.Command => _command.Command;

            object? IExecutedCommand.Result => _command.Result;
        }

        /// <summary>
        /// Gets the strongly typed command and its result.
        /// This must be called only for <see cref="ICommand{TResult}"/> otherwise
        /// an <see cref="ArgumentException"/> is thrown.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <returns>A strongly typed command and its result.</returns>
        public IExecutedCommand<T>.WithResult<TResult> WithResult<TResult>()
        {
            // Building a strongly typed result: we check that the actual result type (that is
            // the most precise type among the different ICommand<TResult> TResult types) is
            // compatible with the requested TResult.
            ExecutingCommand<T>.CheckResultType<TResult>( base.Command.CrisPocoModel );
            return new ResultAdapter<TResult>( this );
        }

    }

}
