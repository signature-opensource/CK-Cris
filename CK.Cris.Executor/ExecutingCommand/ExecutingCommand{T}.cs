using CK.Core;
using CK.Cris;
using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Implements <see cref="IExecutingCommand{T}"/>.
    /// </summary>
    /// <typeparam name="T">The command type to be executed.</typeparam>
    public sealed class ExecutingCommand<T> : ExecutingCommand, IExecutingCommand<T> where T : class, IAbstractCommand
    {
        /// <summary>
        /// Initializes a new executing command.
        /// </summary>
        /// <param name="command">The command that will be executed.</param>
        /// <param name="issuerToken">The correlation token.</param>
        public ExecutingCommand( T command, ActivityMonitor.Token issuerToken )
            : base( command, issuerToken )
        {
        }

        /// <inheritdoc />
        public new T Command => Unsafe.As<T>( base.Command );

        sealed class ResultAdapter<TResult> : IExecutingCommand<T>.WithResult<TResult>
        {
            readonly ExecutingCommand<T> _command;
            readonly TaskCompletionSource<TResult> _result;

            public ResultAdapter( ExecutingCommand<T> command )
            {
                _command = command;
                _result = new TaskCompletionSource<TResult>();
                _ = _command.SafeCompletion.ContinueWith( OnRequestCompletion!,
                                                             _result,
                                                             CancellationToken.None,
                                                             TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default );
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage( "Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "This is called on a completed task." )]
            static void OnRequestCompletion( Task<object?> c, object target )
            {
                var result = (TaskCompletionSource<TResult>)target;
                // Don't take any risk: even if there should not be Faulted or Canceled state
                // on the CommandCompletion, transfers it if it happens.
                if( c.Exception != null ) result.SetException( c.Exception );
                else if( c.IsCanceled ) result.SetCanceled();
                else
                {
                    // If the completion is a ICrisResultError, resolves the result task with an exception.
                    object? r = c.Result;
                    // The completion is null or an instance of some type (the most precise type among
                    // the different ICommand<TResult> TResult types. It may be a ICrisResultError and if
                    // the TResult is a ICrisResultError this is fine:
                    // Fast path is that the result is assignable.
                    if( r is TResult typedResult )
                    {
                        result.SetResult( typedResult );
                    }
                    else if( r is ICrisResultError error )
                    {
                        // The result is a ICrisResultError: we set an exception on the Task.
                        var ex = new CKException( $"Command failed with {error.Messages.Count} messages." );
                        result.SetException( ex );
                    }
                    else
                    {
                        // No error, the completion is null or an instance of non assignable type.
                        // Fast path is that the result type is fine.
                        // If TResult allows null, it's fine (the trick is to use the default(T) here).
                        if( r == null )
                        {
                            if( default( TResult ) == null )
                            {
                                result.SetResult( default( TResult )! );
                            }
                            else
                            {
                                var ex = new CKException( $"Request result is null. This is not compatible with '{typeof( TResult ).ToCSharpName()}'." );
                                result.SetException( ex );
                            }
                        }
                        else
                        {
                            var ex = new CKException( $"Request result is a '{r.GetType().ToCSharpName()}'. This is not compatible with '{typeof( TResult ).ToCSharpName()}'." );
                            result.SetException( ex );
                        }
                    }
                }
            }

            public Task<TResult> Result => _result.Task;

            public T Command => _command.Command;

            public Task<CrisValidationResult> ValidationResult => _command.ValidationResult;

            public ImmediateEvents ImmediateEvents => _command.ImmediateEvents;

            IAbstractCommand IExecutingCommand.Command => _command.Command;

            public ActivityMonitor.Token IssuerToken => _command.IssuerToken;

            public DateTime CreationDate => _command.CreationDate;

            public Task<object?> SafeCompletion => _command.SafeCompletion;

            public Task<object?> Completion => _command.Completion;

            public IReadOnlyList<IEvent> Events => _command.Events;
        }

        /// <summary>
        /// Gets the strongly typed command and its result.
        /// This must be called only for <see cref="ICommand{TResult}"/> otherwise
        /// an <see cref="ArgumentException"/> is thrown.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <returns>A strongly typed command and its result.</returns>
        public IExecutingCommand<T>.WithResult<TResult> WithResult<TResult>()
        {
            // Building a strongly typed result: we check that the actual result type (that is
            // the most precise type among the different ICommand<TResult> TResult types) is
            // compatible with the requested TResult.
            ExecutedCommand<T>.CheckResultType<TResult>( base.Command.CrisPocoModel );
            return new ResultAdapter<TResult>( this );
        }

    }

}
