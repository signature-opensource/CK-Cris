using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.Cris;

/// <summary>
/// Optional service that creates a DI scope whose ambient services are the ones of a <see cref="AmbientServiceHub"/>,
/// to execute a command inline (on the caller's asynchronous flow: no job, no runner, so nothing can deadlock) with
/// other ambient services. See <see cref="CrisExecutionContext.RawExecuteInlineAsync(AmbientServiceHub, IAbstractCommand)"/>.
/// <para>
/// It is implemented by CK.Cris.BackgroundExecutor with its background container: the container built from a hub.
/// </para>
/// </summary>
public interface ICrisAmbientScopeFactory : ISingletonAutoService
{
    /// <summary>
    /// Creates a scope whose ambient services are the ones of the <paramref name="hub"/>. Its <see cref="IActivityMonitor"/>
    /// is <paramref name="monitor"/> and its <see cref="ICrisCommandContext"/> (and <see cref="ICrisEventContext"/>) is
    /// the one created by <paramref name="contextFactory"/> with the scope's services.
    /// </summary>
    /// <param name="hub">The ambient services.</param>
    /// <param name="monitor">The monitor of the scope.</param>
    /// <param name="contextFactory">Creates the execution context of the scope from its services.</param>
    /// <returns>The scope to dispose once the execution is over.</returns>
    AsyncServiceScope CreateScope( AmbientServiceHub hub, IActivityMonitor monitor, Func<IServiceProvider, CrisExecutionContext> contextFactory );
}
