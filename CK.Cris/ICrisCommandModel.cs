using CK.Core;
using System.Collections.Immutable;

namespace CK.Cris;

/// <summary>
/// Specialized <see cref="ICrisPocoModel"/> for commands.
/// </summary>
public interface ICrisCommandModel : ICrisPocoModel
{
    /// <summary>
    /// Creates a <see cref="ExecutedCommand"/> that has the right <see cref="IExecutedCommand{T}"/> interface
    /// for this command type.
    /// </summary>
    /// <param name="command">The command instance. Its <see cref="ICrisPoco.CrisPocoModel"/> must be this model.</param>
    /// <param name="result">The command result. See <see cref="IExecutedCommand.Result"/>.</param>
    /// <param name="validationMessages">The command validation messages.</param>
    /// <param name="events">The non immediate events emitted by the command.</param>
    /// <param name="deferredExecutionInfo">
    /// Optional <see cref="IDeferredCommandExecutionContext"/> that must be set when the command execution
    /// has been deferred to another context.
    /// </param>
    /// <returns>A correctly typed <see cref="ExecutedCommand"/>.</returns>
    ExecutedCommand CreateExecutedCommand( IAbstractCommand command,
                                           object? result,
                                           ImmutableArray<UserMessage> validationMessages,
                                           ImmutableArray<IEvent> events,
                                           IDeferredCommandExecutionContext? deferredExecutionInfo );
}
