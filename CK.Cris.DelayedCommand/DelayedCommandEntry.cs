using CK.Core;
using System.Threading.Tasks;

namespace CK.Cris;

/// <summary>
/// Captures a <see cref="IDelayedCommand"/> that is waiting for its execution.
/// <para>
/// The ambient services of the delayed command are restored from the command when it is executed; the entry keeps the
/// <see cref="CapturedAmbientValues"/> of the storing context (plain values, never services) and completes the
/// restored ones with them.
/// </para>
/// </summary>
public sealed class DelayedCommandEntry : IDeferredCommandExecutionContext, IRestoredAmbientServicesCompleter
{
    readonly int _seqId;
    readonly ActivityMonitor.Token _issuerToken;
    readonly IDelayedCommand _delayedCommand;
    readonly TaskCompletionSource<IExecutingCommand> _executing;
    readonly CapturedAmbientValues? _capturedValues;

    internal DelayedCommandEntry( int seqId, ActivityMonitor.Token issuerToken, IDelayedCommand delayedCommand, CapturedAmbientValues? capturedValues )
    {
        _capturedValues = capturedValues;
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
    /// Gets the ambient values captured when the command has been stored. Null when there is nothing to complete.
    /// </summary>
    public CapturedAmbientValues? CapturedValues => _capturedValues;

    void IRestoredAmbientServicesCompleter.Complete( IActivityMonitor monitor, AmbientServiceHub hub ) => _capturedValues?.Complete( monitor, hub );

    /// <summary>
    /// Gets a task that will be completed when the <see cref="IDelayedCommand.Command"/> has been submitted
    /// to the background executor (the command is queued, its execution may not have started yet).
    /// <see cref="IExecutingCommand.ExecutedCommand"/> can then be used to await the command completion.
    /// </summary>
    public Task<IExecutingCommand> ExecutingCommand => _executing.Task;

    internal void SetExecuting( IExecutingCommand executingCommand ) => _executing.SetResult( executingCommand );
}
