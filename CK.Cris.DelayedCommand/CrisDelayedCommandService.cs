using CK.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Cris;

/// <summary>
/// Simple basic implementation of an in-memory only service that handles <see cref="IDelayedCommand"/>.
/// <para>
/// This implementation is open to extension: by overriding <see cref="StoreAsync(IActivityMonitor, IDelayedCommand, ActivityMonitor.Token?)"/>
/// <see cref="OnCommandExecuting(DelayedCommandEntry)"/> (and possibly the [CommandHandler] <see cref="HandleCommandAsync(IActivityMonitor, IDelayedCommand)"/>),
/// a more complex implementation can handle persistence.
/// </para>
/// </summary>
public class CrisDelayedCommandService : ISingletonAutoService
{
    readonly PriorityQueue<DelayedCommandEntry, DateTime> _memoryStore;
    readonly Timer _timer;
    readonly CrisBackgroundExecutorService _backgroundExecutorService;
    readonly PocoDirectory _pocoDirectory;
    readonly RawCrisExecutor _rawCrisExecutor;
    int _seqNum;
    DateTime _nextDate;
    const uint _maxTimer = uint.MaxValue - 1;

    /// <summary>
    /// Initializes a new <see cref="CrisDelayedCommandService"/>.
    /// </summary>
    /// <param name="backgroundExecutorService">The background executor service.</param>
    /// <param name="pocoDirectory">The Poco directory.</param>
    /// <param name="rawCrisExecutor">The Cris executor.</param>
    public CrisDelayedCommandService( CrisBackgroundExecutorService backgroundExecutorService,
                                      PocoDirectory pocoDirectory,
                                      RawCrisExecutor rawCrisExecutor )
    {
        _memoryStore = new PriorityQueue<DelayedCommandEntry, DateTime>();
        _timer = new Timer( OnTimer );
        _nextDate = Util.UtcMaxValue;
        _backgroundExecutorService = backgroundExecutorService;
        _pocoDirectory = pocoDirectory;
        _rawCrisExecutor = rawCrisExecutor;
    }

    DateTime GetUtcNow() => DateTime.UtcNow;

    /// <summary>
    /// Core method that stores the command in the in-memory priority queue and manages the timer: the stored
    /// command will eventually be submitted to the <see cref="CrisBackgroundExecutorService"/> and <see cref="OnCommandExecuting(DelayedCommandEntry)"/>
    /// will be called.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The delayed command.</param>
    /// <param name="issuerToken">Optional already obtained correlation token.</param>
    /// <returns>A 1-based ever incremented sequence number that identifies the added command.</returns>
    protected int MemoryStore( IActivityMonitor monitor, IDelayedCommand command, ActivityMonitor.Token? issuerToken = null )
    {
        Throw.CheckArgument( command.Command is not null );
        var innerCommand = command.Command;
        issuerToken ??= monitor.CreateToken( $"Delayed execution command: '{innerCommand.CrisPocoModel.PocoName}' at '{command.ExecutionDate}'." );
        lock( _memoryStore )
        {
            var entry = new DelayedCommandEntry( ++_seqNum, issuerToken, command );
            _memoryStore.Enqueue( entry, command.ExecutionDate );
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

    void OnTimer( object? _ )
    {
        lock( _memoryStore )
        {
            bool hasMore = false;
            while( _memoryStore.TryPeek( out var entry, out var date ) )
            {
                var delta = (long)(date - GetUtcNow()).TotalMilliseconds;
                if( delta <= 0 )
                {
                    entry.SetExecuting( _backgroundExecutorService.Submit( entry.DelayedCommand.Command!,
                                                                           ambientServiceHub: null,
                                                                           entry.IssuerToken,
                                                                           entry,
                                                                           OnExecutedCommandAsync,
                                                                           incomingValidationCheck: true ) );
                    _memoryStore.Dequeue();
                    OnCommandExecuting( entry );
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
    /// Extension point called when command is submitted to the executor.
    /// <para>
    /// Caution: This is called from the timer thread while an internal lock is held on the memory store.
    /// Whatever is implemented here must be fast.
    /// </para>
    /// <para>
    /// Does nothing by default.
    /// </para>
    /// </summary>
    /// <param name="entry">The starting command.</param>
    protected virtual void OnCommandExecuting( DelayedCommandEntry entry )
    {
    }

    /// <summary>
    /// Dispatches a <see cref="IDelayedCommandExecutedEvent"/> for the event.
    /// </summary>
    /// <param name="monitor">The monitor (from the <see cref="CrisExecutionHost"/>'s runner.</param>
    /// <param name="command">The executed command (can be on error).</param>
    /// <param name="services">
    /// The configured services used by the command execution.
    /// This is null if and only if the scoped services could not be created because ambient services
    /// failed to be restored (a [RestoreAmblientService] methods threw an exception).
    /// </param>
    /// <returns>The awaitable.</returns>
    protected Task OnExecutedCommandAsync( IActivityMonitor monitor, IExecutedCommand command, IServiceProvider? services )
    {
        if( services == null )
        {
            monitor.Warn( ActivityMonitor.Tags.ToBeInvestigated, $"Restore ambient service failed. No IDelayedCommandExecutedEvent is raised." );
            return Task.CompletedTask;
        }
        // The entry can be retrived directly if needed:
        // var entry = (DelayedCommandEntry)command.DeferredExecutionContext!;
        return _rawCrisExecutor.SafeDispatchEventAsync( services, _pocoDirectory.Create<IDelayedCommandExecutedEvent>( d => d.Initialize( command ) ) );
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
