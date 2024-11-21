using CK.Core;
using System.Threading.Tasks;

namespace CK.Cris;

/// <summary>
/// Captures a <see cref="IDelayedCommand"/> that is waiting for its execution.
/// </summary>
public sealed class DelayedCommandEntry : IDeferredCommandExecutionContext
{
    readonly int _seqId;
    readonly ActivityMonitor.Token _issuerToken;
    readonly IDelayedCommand _delayedCommand;
    readonly TaskCompletionSource<IExecutingCommand> _executing;

    internal DelayedCommandEntry( int seqId, ActivityMonitor.Token issuerToken, IDelayedCommand delayedCommand )
    {
        _seqId = seqId;
        _issuerToken = issuerToken;
        _delayedCommand = delayedCommand;
        _executing = new TaskCompletionSource<IExecutingCommand>( TaskCreationOptions.RunContinuationsAsynchronously );
    }

    /// <summary>
    /// Gets a token that identifies the initialization of the command execution.
    /// It is either provided to <see cref="CrisDelayedCommandService.StoreAsync(IActivityMonitor, IDelayedCommand, ActivityMonitor.Token?)"/>
    /// or created by <see cref="CrisDelayedCommandService.MemoryStore(IActivityMonitor, IDelayedCommand, ActivityMonitor.Token?)"/>.
    /// </summary>
    public ActivityMonitor.Token IssuerToken => _issuerToken;

    /// <summary>
    /// A 1-based ever incremented sequence number that identifies the command in the in-memory store.
    /// </summary>
    public int MemorySequenceId => _seqId;

    /// <summary>
    /// Gets the delayed command.
    /// </summary>
    public IDelayedCommand DelayedCommand => _delayedCommand;

    /// <summary>
    /// Gets a task that will be completed when the <see cref="IDelayedCommand.Command"/> is starting its execution.
    /// <see cref="IExecutingCommand.Command"/> can then be used to await the command completion.
    /// </summary>
    public Task<IExecutingCommand> ExecutingCommand => _executing.Task;

    internal void SetExecuting( IExecutingCommand executingCommand ) => _executing.SetResult( executingCommand );
}
