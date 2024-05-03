using CK.Core;
using CK.Cris;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Handles [AmbientServiceValue]. Checks that the property is nullable and
    /// registers the property in the CrisTypeRegistry.
    /// </summary>
    sealed class AmbientServiceValueAttributeImpl : ICSCodeGenerator
    {
        readonly Type _type;
        readonly PropertyInfo _prop;

        public AmbientServiceValueAttributeImpl( AmbientServiceValueAttribute attr, Type t, PropertyInfo p )
        {
            _type = t;
            _prop = p;
        }

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext c )
        {
            // Wait for CrisTypeRegistry to be available.
            var crisTypeRegistry = c.CurrentRun.ServiceContainer.GetService<CrisTypeRegistry>();
            if( crisTypeRegistry == null ) return CSCodeGenerationResult.Retry;

            if( crisTypeRegistry.CrisPocoType == null )
            {
                monitor.Warn( $"AmbientService value '{_type:C}.{_prop.Name}' ignored as there are no CrisPoco types registered." );
                return CSCodeGenerationResult.Success;
            }
            var ownerType = crisTypeRegistry.TypeSystem.FindByType( _type );
            if( ownerType == null || ownerType.ImplementationLess )
            {
                monitor.Trace( $"AmbientService value '{_type:C}.{_prop.Name}' ignored as the defining type is unused." );
                return CSCodeGenerationResult.Success;
            }
            ownerType = ownerType.NonNullable;
            if( ownerType is ISecondaryPocoType s ) ownerType = s.PrimaryPocoType;
            if( ownerType.Kind is not PocoTypeKind.PrimaryPoco and not PocoTypeKind.AbstractPoco
                || !ownerType.CanReadFrom( crisTypeRegistry.CrisPocoType ) )
            {
                monitor.Error( $"Invalid [AmbientServiceValue] '{ownerType.CSharpName}.{_prop.Name}' on {ownerType.Kind}. Only ICrisPoco properties can be AmbientService values." );
                return CSCodeGenerationResult.Failed;
            }
            // The owner can be a Primary or an Abstract Poco type. The [AmbientServiceValue] field is
            // a IBasePocoField.
            IBaseCompositeType owner = (IBaseCompositeType)ownerType;
            var f = owner.Fields.FirstOrDefault( f => f.Name == _prop.Name );
            if( f == null )
            {
                // This should not happen. Defensive programming here.
                monitor.Error( $"[AmbientServiceValue] '{ownerType.CSharpName}.{_prop.Name}' doesn't appear on '{ownerType}'. Available fields are: " +
                                $"{owner.Fields.Select( f => f.Name ).Concatenate()}." );
                return CSCodeGenerationResult.Failed;
            }
            if( !f.Type.IsNullable )
            {
                monitor.Error( $"[AmbientServiceValue] '{f.Type.CSharpName} {ownerType.CSharpName}.{f.Name}' must be nullable. Ambient values must always be nullable." );
                return CSCodeGenerationResult.Failed;
            }
            return crisTypeRegistry.RegisterAmbientValueDefinitionField( monitor, owner, f )
                        ? CSCodeGenerationResult.Success
                        : CSCodeGenerationResult.Failed;
        }

    }
}
