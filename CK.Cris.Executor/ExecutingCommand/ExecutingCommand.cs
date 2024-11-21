using CK.Core;
using System.Threading.Tasks;

namespace CK.Cris;

/// <summary>
/// Non generic base class for <see cref="ExecutingCommand{T}"/>.
/// </summary>
public class ExecutingCommand : IExecutingCommand, IDarkSideExecutingCommand
{
    readonly IAbstractCommand _command;
    readonly ActivityMonitor.Token _issuerToken;
    readonly TaskCompletionSource<IExecutedCommand> _completion;
    readonly internal ImmediateEvents _immediate;

    internal ExecutingCommand( IAbstractCommand command,
                               ActivityMonitor.Token issuerToken )
    {
        _issuerToken = issuerToken;
        _command = command;
        _completion = new TaskCompletionSource<IExecutedCommand>( TaskCreationOptions.RunContinuationsAsynchronously );
        // This is useless because this task is never faulted:
        //    Avoid Unobserved exception on this task.
        //    _ = _completion.Task.ContinueWith( t => t.Exception!.Handle( ex => true ), default, TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default );
        _immediate = new ImmediateEvents();
    }

    /// <inheritdoc />
    public IAbstractCommand Command => _command;

    /// <inheritdoc />
    public ActivityMonitor.Token IssuerToken => _issuerToken;

    /// <inheritdoc />
    public Task<IExecutedCommand> ExecutedCommand => _completion.Task;

    /// <summary>
    /// Gets a live collection of events emitted by the command execution.
    /// <para>
    /// This is a thread safe collection that is updated during the execution.
    /// The <see cref="ImmediateEvents.Added"/> event is raised only for <see cref="CrisPocoKind.RoutedImmediateEvent"/> event.
    /// (see <see cref="ICrisPoco.CrisPocoModel"/>.<see cref="ICrisPocoModel.Kind">Kind</see>).
    /// </para>
    /// </summary>
    public ImmediateEvents ImmediateEvents => _immediate;

    /// <summary>
    /// Gets the dark side of this executing command.
    /// </summary>
    public IDarkSideExecutingCommand DarkSide => this;

    Task IDarkSideExecutingCommand.AddImmediateEventAsync( IActivityMonitor monitor, IEvent e )
    {
        return _immediate.AddAndRaiseAsync( monitor, e );
    }

    void IDarkSideExecutingCommand.SetResult( IExecutedCommand result )
    {
        _completion.SetResult( result );
    }
}

