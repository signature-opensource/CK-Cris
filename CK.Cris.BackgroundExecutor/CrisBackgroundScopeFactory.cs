using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.Cris;

/// <summary>
/// Implements <see cref="ICrisAmbientScopeFactory"/> with the background container: the container built from a
/// <see cref="AmbientServiceHub"/>. The scope is not bound to a job: the command executes inline, on the caller's
/// asynchronous flow.
/// </summary>
public sealed class CrisBackgroundScopeFactory : ICrisAmbientScopeFactory
{
    readonly IDIContainer<CrisBackgroundDIContainerDefinition.Data> _container;

    /// <summary>
    /// Initializes a new <see cref="CrisBackgroundScopeFactory"/>.
    /// </summary>
    /// <param name="container">The background container.</param>
    public CrisBackgroundScopeFactory( IDIContainer<CrisBackgroundDIContainerDefinition.Data> container )
    {
        _container = container;
    }

    /// <inheritdoc />
    public AsyncServiceScope CreateScope( AmbientServiceHub hub, IActivityMonitor monitor, Func<IServiceProvider, CrisExecutionContext> contextFactory )
    {
        Throw.CheckNotNullArgument( hub );
        Throw.CheckNotNullArgument( monitor );
        Throw.CheckNotNullArgument( contextFactory );
        var data = new CrisBackgroundDIContainerDefinition.Data( hub ) { _inlineMonitor = monitor };
        var scope = _container.GetContainer().CreateAsyncScope( data );
        data._inlineContext = contextFactory( scope.ServiceProvider );
        return scope;
    }
}
