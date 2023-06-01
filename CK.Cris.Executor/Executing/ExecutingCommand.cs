using CK.Core;
using CK.Cris;
using CK.PerfectEvent;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Non generic base class for <see cref="IExecutingCommand{T}"/> implementation.
    /// </summary>
    public class ExecutingCommand : IExecutingCommand
    {
        readonly IAbstractCommand _payload;
        readonly ActivityMonitor.Token _issuerToken;
        readonly TaskCompletionSource<CrisValidationResult> _validation;
        readonly TaskCompletionSource<object?> _completion;
        readonly Collector<IExecutingCommand, IEvent> _events;

        internal ExecutingCommand( IAbstractCommand payload,
                                   ActivityMonitor.Token issuerToken,
                                   PerfectEventSender<IExecutingCommand, IEvent>? onEventRelay )
        {
            _issuerToken = issuerToken;
            _payload = payload;
            _validation = new TaskCompletionSource<CrisValidationResult>();
            _completion = new TaskCompletionSource<object?>();
            _events = new Collector<IExecutingCommand, IEvent>( onEventRelay );
        }

        /// <inheritdoc />
        public IAbstractCommand Payload => _payload;

        /// <inheritdoc />
        public ActivityMonitor.Token IssuerToken => _issuerToken;

        /// <inheritdoc />
        public DateTime CreationDate => _issuerToken.CreationDate.TimeUtc;

        /// <inheritdoc />
        public Task<CrisValidationResult> ValidationResult => _validation.Task;

        /// <inheritdoc />
        public Task<object?> RequestCompletion => _completion.Task;

        /// <inheritdoc />
        public ICollector<IExecutingCommand, IEvent> Events => _events;

        internal bool SetValidationResult( IParallelLogger logger, IPocoFactory<ICrisResultError> errorFactory, CrisValidationResult v )
        {
            _validation.SetResult( v );
            if( !v.Success )
            {
                SetResult( logger, errorFactory.Create( e => e.Errors.AddRange( v.Errors ) ) );
                return true;
            }
            return false;
        }

        internal Task AddCommandEventAsync( IActivityMonitor monitor, IEvent e )
        {
            return _events.AddAsync( monitor, this, e );
        }

        internal void SetResult( IParallelLogger logger, object? result )
        {
            _completion.SetResult( result );
            _events.Close();
        }
    }

}
