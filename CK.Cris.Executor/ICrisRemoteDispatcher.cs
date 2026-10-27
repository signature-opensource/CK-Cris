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
}
