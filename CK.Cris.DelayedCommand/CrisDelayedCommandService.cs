using CK.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Simple basic implementation of an in-memory only service that handles <see cref="IDelayedCommand"/>.
    /// <para>
    /// This implementation is open to extension: by overriding its single <see cref="StoreAsync(IActivityMonitor, IDelayedCommand, ActivityMonitor.Token?)"/>
    /// method, a more complex implementation can handle persistence.
    /// </para>
    /// </summary>
    public class CrisDelayedCommandService : ISingletonAutoService
    {
        readonly PriorityQueue<(ICommand C, int S, ActivityMonitor.Token T), DateTime> _memoryStore;
        readonly Timer _timer;
        readonly CrisBackgroundExecutorService _backgroundExecutorService;
        int _seqNum;
        DateTime _nextDate;
        const uint _maxTimer = uint.MaxValue - 1;

        public CrisDelayedCommandService( CrisBackgroundExecutorService backgroundExecutorService )
        {
            _memoryStore = new PriorityQueue<(ICommand, int, ActivityMonitor.Token), DateTime>();
            _timer = new Timer( OnTimer );
            _nextDate = Util.UtcMaxValue;
            _backgroundExecutorService = backgroundExecutorService;
        }

        DateTime GetUtcNow() => DateTime.UtcNow; 

        void OnTimer( object? _ )
        {
            lock( _memoryStore )
            {
                bool hasMore = false;
                while( _memoryStore.TryPeek( out var ready, out var date ) )
                {
                    var delta = (long)(date - GetUtcNow()).TotalMilliseconds;
                    if( delta <= 0 )
                    {
                        var executing = _backgroundExecutorService.Submit( ready.C, ambientServiceHub: null, ready.T, incomingValidationCheck: true );
                        _memoryStore.Dequeue();
                        OnCommandSubmitted( ready.S, executing );
                    }
                    else
                    {
                        _nextDate = date;
                        if( delta >= _maxTimer ) delta = _maxTimer;
                        _timer.Change( (uint)delta, 0 );
                        hasMore = true;
                        break;
                    }
                }
                if( !hasMore ) _nextDate = Util.UtcMaxValue;
            }
        }

        /// <summary>
        /// Extension point called for each submitted command.
        /// <para>
        /// Caution: This is called from the timer thread while an internal lock is held on the memory store.
        /// Whatver is implemented here must be fast.
        /// </para>
        /// <para>
        /// Does nothing by default.
        /// </para>
        /// </summary>
        /// <param name="commandId">The 1-based ever increasing sequence number returned by <see cref="MemoryStore(IActivityMonitor, IDelayedCommand, ActivityMonitor.Token?)"/>.</param>
        /// <param name="executing">The executing command.</param>
        protected virtual void OnCommandSubmitted( int commandId, IExecutingCommand<ICommand> executing )
        {
        }

        /// <summary>
        /// Core method that stores the command in the in-memory priority queue and manages the timer: the stored
        /// command will eventually be submotted to the <see cref="CrisBackgroundExecutorService"/> and <see cref="OnCommandSubmitted(int, IExecutingCommand{ICommand})"/>
        /// will be called.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The delayed command.</param>
        /// <param name="issuerToken">Optional already obtained correlation token.</param>
        /// <returns>An ever 1-based incremented sequence number that identifies the added command.</returns>
        protected int MemoryStore( IActivityMonitor monitor, IDelayedCommand command, ActivityMonitor.Token? issuerToken = null )
        {
            Throw.CheckArgument( command.Command is not null );
            var innerCommand = command.Command;
            issuerToken ??= monitor.CreateToken( $"Delayed execution command: '{innerCommand.CrisPocoModel.PocoName}' at '{command.ExecutionDate}'." );
            lock( _memoryStore )
            {
                _memoryStore.Enqueue( (innerCommand, ++_seqNum, issuerToken), command.ExecutionDate );
                if( command.ExecutionDate < _nextDate )
                {
                    _nextDate = command.ExecutionDate;
                    var delta = (long)(_nextDate - GetUtcNow()).TotalMilliseconds;
                    if( delta <= 0 ) delta = 0;
                    else if( delta > _maxTimer ) delta = _maxTimer;
                    _timer.Change( (uint)delta, 0 );
                }
                return _seqNum;
            }
        }

        /// <summary>
        /// Simple relay to <see cref="MemoryStore(IActivityMonitor, IDelayedCommand, ActivityMonitor.Token?)"/>.
        /// More complex specializations should override this.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The delayed command.</param>
        /// <param name="issuerToken">Optional already obtained correlation token.</param>
        /// <returns>The awaitable.</returns>
        public virtual ValueTask StoreAsync( IActivityMonitor monitor, IDelayedCommand command, ActivityMonitor.Token? issuerToken = null )
        {
            Throw.CheckArgument( command.Command is not null );
            MemoryStore( monitor, command, issuerToken );
            if( !command.KeepOnlyInMemory )
            {
                monitor.Warn( $"Unable to persist command '{command.CrisPocoModel.PocoName}' since this is an in-memory only store." );
            }
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Checks <see cref="IDelayedCommand.Command"/> is valid and that if <see cref="IDelayedCommand.AllowPastExecutionDate"/> is false,
        /// the <see cref="IDelayedCommand.ExecutionDate"/> is in the future.
        /// </summary>
        /// <param name="c">The validation context.</param>
        /// <param name="command">The delayed command</param>
        [IncomingValidator]
        public ValueTask ValidateCommandAsync( ICrisIncomingValidationContext c, IDelayedCommand command )
        {
            // Temporary: [NullInvalid] will do the job.
            if( command.Command == null )
            {
                c.Messages.Error( "Invalid null Command property." );
                return ValueTask.CompletedTask;
            }
            if( !command.AllowPastExecutionDate )
            {
                var now = GetUtcNow();
                if( command.ExecutionDate < now )
                {
                    c.Messages.Error( $"Delayed command for '{command.Command.CrisPocoModel.PocoName}' is in past: ExecutionDate is '{command.ExecutionDate}', UtcNow is '{now}'." );
                }
            }
            return c.ValidateAsync( command.Command );
        }

        /// <summary>
        /// Simply calls <see cref="StoreAsync(IActivityMonitor, IDelayedCommand, ActivityMonitor.Token?)"/>.
        /// Commands are always executed from the background context even if their <see cref="IDelayedCommand.ExecutionDate"/> is in the past.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The delayed command.</param>
        /// <returns>The awaitable.</returns>
        [CommandHandler]
        public virtual ValueTask HandleCommandAsync( IActivityMonitor monitor, IDelayedCommand command )
        {
            return StoreAsync( monitor, command );
        }

    }
}
