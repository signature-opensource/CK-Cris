using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace CK.Cris
{
    /// <summary>
    /// Implements a typed command <see cref="IExecutedCommand{T}"/>.
    /// </summary>
    /// <typeparam name="T">The command type.</typeparam>
    public sealed class ExecutedCommand<T> : ExecutedCommand, IExecutedCommand<T> where T : class, IAbstractCommand
    {
        /// <summary>
        /// Initializes a new <see cref="ExecutedCommand{T}"/>.
        /// </summary>
        /// <param name="command">The executed command.</param>
        /// <param name="result">The command result.</param>
        /// <param name="validationMessages">The validation messages.</param>
        /// <param name="events">The emitted events.</param>
        public ExecutedCommand( T command, object? result, ImmutableArray<UserMessage> validationMessages, ImmutableArray<IEvent> events )
            : base( command, result, validationMessages, events )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ExecutedCommand{T}"/>.
        /// </summary>
        /// <param name="command">The executed command.</param>
        /// <param name="result">The command result.</param>
        /// <param name="validationMessages">The validation messages.</param>
        /// <param name="events">The emitted events.</param>
        public ExecutedCommand( T command, object? result, IEnumerable<UserMessage>? validationMessages = null, IEnumerable<IEvent>? events = null )
            : base( command, result, validationMessages, events )
        {
        }

        /// <inheritdoc />
        public new T Command => Unsafe.As<T>( base.Command );

        sealed class ResultAdapter<TResult> : IExecutedCommand<T>.IWithResult<TResult>
        {
            readonly ExecutedCommand<T> _command;

            public ResultAdapter( ExecutedCommand<T> command )
            {
                _command = command;
            }

            public TResult Result => (TResult)_command.Result!;

            public T Command => _command.Command;

            public ImmutableArray<UserMessage> ValidationMessages => _command.ValidationMessages;

            public ImmutableArray<IEvent> Events => _command.Events;

            IAbstractCommand IExecutedCommand.Command => _command.Command;

            object? IExecutedCommand.Result => _command.Result;

            public IExecutedCommand<T>.IWithResult<TOtherResult> WithResult<TOtherResult>() => _command.WithResult<TOtherResult>();
        }

        /// <inheritdoc />
        public IExecutedCommand<T>.IWithResult<TResult> WithResult<TResult>()
        {
            // Building a strongly typed result: we check that the actual result type (that is
            // the most precise type among the different ICommand<TResult> TResult types) is
            // compatible with the requested TResult.
            CheckResultType<TResult>( base.Command.CrisPocoModel );
            return new ResultAdapter<TResult>( this );
        }

        /// <summary>
        /// Helper that checks the final result type and throws <see cref="ArgumentException"/> if the
        /// requested result type is not valid.
        /// </summary>
        /// <typeparam name="TResult">The requested result type.</typeparam>
        /// <param name="model">The command model.</param>
        public static void CheckResultType<TResult>( ICrisPocoModel model )
        {
            var requestedType = typeof( TResult );
            if( !requestedType.IsAssignableFrom( model.ResultType ) )
            {
                if( model.ResultType == typeof( void ) )
                {
                    Throw.ArgumentException( $"Command '{model.PocoName}' is a ICommand (without any result)." );
                }
                Throw.ArgumentException( $"Command '{model.PocoName}' is a 'ICommand<{model.ResultType.ToCSharpName()}>'." +
                                         $" This type of result is not compatible with '{requestedType.ToCSharpName()}'." );
            }
        }
    }

}
