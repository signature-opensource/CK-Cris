using CK.Core;
using CK.Cris;
using CK.PerfectEvent;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Cris
{
    sealed class ExecutingCommand<T> : ExecutingCommand, IExecutingCommand<T> where T : class, IAbstractCommand
    {
        public ExecutingCommand( T command,
                                 ActivityMonitor.Token issuerToken,
                                 PerfectEventSender<IExecutingCommand, IEvent>? onEventRelay )
            : base( command, issuerToken, onEventRelay )
        {
        }

        public T Command => Unsafe.As<T>( Payload );

        sealed class ResultAdapter<TResult> : IExecutingCommand<T>.WithResult<TResult>
        {
            readonly ExecutingCommand<T> _command;
            readonly TaskCompletionSource<TResult> _result;

            public ResultAdapter( ExecutingCommand<T> command )
            {
                _command = command;
                _result = new TaskCompletionSource<TResult>();
                _ = _command.RequestCompletion.ContinueWith( OnRequestCompletion!,
                                                             _result,
                                                             CancellationToken.None,
                                                             TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default );
            }

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
                    var r = c.Result;
                    if( r is ICrisResultError error )
                    {
                        var ex = new CKException( $"Request failed with {error.Errors.Count} errors." );
                        result.SetException( ex );
                    }
                    else
                    {
                        // No error, the completion is null or an instance of some type (the most precise type among
                        // the different ICommand<TResult> TResult types.
                        // Fast path is that the result type is fine.
                        if( r is TResult typedResult )
                        {
                            result.SetResult( typedResult );
                        }
                        else
                        {
                            // What's this type?
                            // If TResult allows it, it's fine (the trick is to use the default(T) here).
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
            }

            public Task<TResult> Result => _result.Task;

            public T Command => _command.Command;

            public Task<CrisValidationResult> ValidationResult => _command.ValidationResult;

            public ICollector<IExecutingCommand, IEvent> Events => _command.Events;

            public IAbstractCommand Payload => _command.Payload;

            public ActivityMonitor.Token IssuerToken => _command.IssuerToken;

            public DateTime CreationDate => _command.CreationDate;

            public Task<object?> RequestCompletion => _command.RequestCompletion;
        }

        public IExecutingCommand<T>.WithResult<TResult> WithResult<TResult>()
        {
            // Building a strongly typed result: we check that the actual result type (that is
            // the most precise type among the different ICommand<TResult> TResult types) is
            // compatible with the requested TResult.
            var requestedType = typeof( TResult );
            if( !requestedType.IsAssignableFrom( Payload.CrisPocoModel.ResultType ) )
            {
                if( Payload.CrisPocoModel.ResultType == typeof( void ) )
                {
                    Throw.ArgumentException( $"Command '{Payload.CrisPocoModel.PocoName}' is a ICommand (without any result)." );
                }
                Throw.ArgumentException( $"Command '{Payload.CrisPocoModel.PocoName}' is a 'ICommand<{Payload.CrisPocoModel.ResultType.ToCSharpName()}>'." +
                                         $" This type of result is not compatible with '{requestedType.ToCSharpName()}'." );
            }
            return new ResultAdapter<TResult>( this );
        }

    }

}
