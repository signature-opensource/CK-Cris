using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;

namespace CK.Setup.Cris;

/// <summary>
/// Base class for CommandHandlingValidatorAttributeImpl, CommandHandlerAttributeImpl, CommandPostHandlerAttributeImpl
/// and RoutedEventHandlerAttributeImpl.
/// </summary>
abstract class BaseHandlerAttributeImpl : ICSCodeGenerator
{
    readonly Type _type;
    readonly MethodInfo _method;

    public BaseHandlerAttributeImpl( Type t, MethodInfo m )
    {
        _type = t;
        _method = m;
    }

    string AttributeName
    {
        get
        {
            var n = GetType().Name;
            Throw.DebugAssert( "AttributeImpl".Length == 13 );
            return n.Remove( n.Length - 13 );
        }
    }

    public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext c )
    {
        var crisTypeRegistry = c.CurrentRun.ServiceContainer.GetService<CrisTypeRegistry>();
        if( crisTypeRegistry == null ) return CSCodeGenerationResult.Retry;

        if( !_method.IsPublic )
        {
            monitor.Error( $"Method '{_type.FullName}.{_method.Name}' that is a [{AttributeName}] must be public." );
            return CSCodeGenerationResult.Failed;
        }
        IStObjFinalClass? impl = c.CurrentRun.EngineMap.ToLeaf( _type );
        if( impl == null )
        {
            monitor.Warn( $"Ignoring method '[{AttributeName}] {_type.FullName:C}.{_method.Name}'. Type is not a Auto Service or a Real Object." );
            return CSCodeGenerationResult.Success;
        }
        return DoImplement( monitor, c.CurrentRun.EngineMap, crisTypeRegistry, impl, _method );
    }

    private protected abstract CSCodeGenerationResult DoImplement( IActivityMonitor monitor,
                                                                   IStObjMap engineMap,
                                                                   CrisTypeRegistry crisTypeRegistry,
                                                                   IStObjFinalClass impl,
                                                                   MethodInfo method );
}
