using CK.Core;
using CK.Cris;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Handles [EndpointValue]. Checks that the property is nullable and
    /// registers it.
    /// </summary>
    sealed class EndpointValueAttributeImpl : ICSCodeGenerator
    {
        readonly Type _type;
        readonly PropertyInfo _prop;

        public EndpointValueAttributeImpl( EndpointValueAttribute attr, Type t, PropertyInfo p )
        {
            _type = t;
            _prop = p;
        }

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext c )
        {
            var crisTypeRegistry = c.CurrentRun.ServiceContainer.GetService<CrisTypeRegistry>();
            if( crisTypeRegistry == null ) return CSCodeGenerationResult.Retry;

            var definer = crisTypeRegistry.TypeSystem.FindByType( _type );
            if( definer == null || definer.ImplementationLess )
            {
                monitor.Trace( $"Endpoint value '{_type:C}.{_prop.Name}' ignored as the defining type is unused." );
                return CSCodeGenerationResult.Success;
            }
            definer = definer.NonNullable;
            if( definer is ISecondaryPocoType s ) definer = s.PrimaryPocoType;
            if( definer.Kind is not PocoTypeKind.PrimaryPoco and not PocoTypeKind.AbstractPoco )
            {
                monitor.Error( $"Invalid [EndpointValue] '{definer.CSharpName}.{_prop.Name}' on {definer.Kind}. Only IPoco fields can be Endpoint values." );
                return CSCodeGenerationResult.Failed;
            }
            IBaseCompositeType owner = (IBaseCompositeType)definer;
            var f = owner.Fields.FirstOrDefault( f => f.Name == _prop.Name );
            if( f == null )
            {
                monitor.Error( $"Endpoint value '{definer.CSharpName}.{_prop.Name}' doesn't appear on '{definer}'. Available fields are: " +
                                $"{owner.Fields.Select( f => f.Name ).Concatenate()}." );
                return CSCodeGenerationResult.Failed;
            }
            if( !f.Type.IsNullable )
            {
                monitor.Error( $"Endpoint value '{f.Type.CSharpName} {definer.CSharpName}.{f.Name}' must be nullable. Endpoint values must always be nullable." );
                return CSCodeGenerationResult.Failed;
            }
            crisTypeRegistry.RegisterEndpointValueDefinitionField( owner, f );
            return CSCodeGenerationResult.Success;
        }

    }
}
