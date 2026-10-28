using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Cris;

/// <summary>
/// Optional service that executes the commands that have no local handler somewhere else. Where a command runs is a
/// deployment detail: the code that sends a command doesn't care whether it is handled in-process or by a remote.
/// <para>
/// It is called by <see cref="RawCrisExecutor.RawExecuteAsync(IServiceProvider, IAbstractCommand)"/> for a command that
/// has no command handler, after its validation, in every execution context (endpoints, background, delayed commands).
/// It is never called for a <see cref="ICommandEndpointOnly"/> command: such a command is executed by its receiving
/// endpoint only. Without implementation, a command without handler is an error.
/// </para>
/// <para>
/// It must fail closed (no route, unreachable or unavailable remote: an error result, never a local execution) and
/// never throw: a <see cref="ICrisResultError"/> is the <see cref="RawCrisExecutor.RawResult.Result"/> on error.
/// </para>
/// </summary>
public interface ICrisRemoteDispatcher : ISingletonAutoService
{
    /// <summary>
    /// Executes a command that has no local handler.
    /// </summary>
    /// <param name="services">The service context of the execution.</param>
    /// <param name="command">The command to execute (it is not a <see cref="ICommandEndpointOnly"/>).</param>
    /// <returns>The raw result.</returns>
    Task<RawCrisExecutor.RawResult> DispatchAsync( IServiceProvider services, IAbstractCommand command );

    /// <summary>
    /// Routes a party command (a <see cref="ICommandTargetParty"/>) before any local execution, even when it has a local
    /// handler: its <see cref="ICommandTargetParty.TargetPartyFullName"/> decides where it executes.
    /// <para>
    /// It is called by <see cref="RawCrisExecutor.RawExecuteAsync(IServiceProvider, IAbstractCommand)"/> before the
    /// handling validation, in every execution context; never for a <see cref="ICommandEndpointOnly"/> command. For a
    /// handled party command, the engine emits the call in its generated code (no runtime type test).
    /// </para>
    /// <para>
    /// Returns null to execute the command here, in the <paramref name="services"/> scope (the target is the current
    /// party). Otherwise, the task is the routed execution (another local party, a remote party, or an error such as "no
    /// route"): like <see cref="DispatchAsync(IServiceProvider, IAbstractCommand)"/>, it must fail closed and never throw.
    /// </para>
    /// <para>
    /// The default implementation returns null: a dispatcher that knows no party executes the party commands here.
    /// </para>
    /// </summary>
    /// <param name="services">The service context of the execution.</param>
    /// <param name="command">The party command.</param>
    /// <returns>Null to execute it here, or the routed execution.</returns>
    Task<RawCrisExecutor.RawResult>? RoutePartyCommand( IServiceProvider services, IAbstractCommand command ) => null;
}
