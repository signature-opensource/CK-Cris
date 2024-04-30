using CK.Core;
using CK.Cris;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Handles [UbiquitousValue]. Checks that the property is nullable and
    /// registers the property in the CrisTypeRegistry.
    /// </summary>
    sealed class UbiquitousValueAttributeImpl : ICSCodeGenerator
    {
        readonly Type _type;
        readonly PropertyInfo _prop;

        public UbiquitousValueAttributeImpl( UbiquitousValueAttribute attr, Type t, PropertyInfo p )
        {
            _type = t;
            _prop = p;
        }

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext c )
        {
            var crisTypeRegistry = c.CurrentRun.ServiceContainer.GetService<CrisTypeRegistry>();
            if( crisTypeRegistry == null ) return CSCodeGenerationResult.Retry;

            var ownerType = crisTypeRegistry.TypeSystem.FindByType( _type );
            if( ownerType == null || ownerType.ImplementationLess || crisTypeRegistry.CrisPocoType == null )
            {
                monitor.Trace( $"Ubiquitous value '{_type:C}.{_prop.Name}' ignored as the defining type is unused." );
                return CSCodeGenerationResult.Success;
            }
            ownerType = ownerType.NonNullable;
            if( ownerType is ISecondaryPocoType s ) ownerType = s.PrimaryPocoType;
            if( ownerType.Kind is not PocoTypeKind.PrimaryPoco and not PocoTypeKind.AbstractPoco
                || !crisTypeRegistry.CrisPocoType.CanReadFrom( ownerType ) )
            {
                monitor.Error( $"Invalid [UbiquitousValue] '{ownerType.CSharpName}.{_prop.Name}' on {ownerType.Kind}. Only ICrisPoco properties can be Ubiquitous values." );
                return CSCodeGenerationResult.Failed;
            }
            // The owner can be a Primary or an Abstract Poco type. The [UbiquitousValue] field is
            // a IBasePocoField.
            IBaseCompositeType owner = (IBaseCompositeType)ownerType;
            var f = owner.Fields.FirstOrDefault( f => f.Name == _prop.Name );
            if( f == null )
            {
                // This should not happen. Defensive programming here.
                monitor.Error( $"Ubiquitous value '{ownerType.CSharpName}.{_prop.Name}' doesn't appear on '{ownerType}'. Available fields are: " +
                                $"{owner.Fields.Select( f => f.Name ).Concatenate()}." );
                return CSCodeGenerationResult.Failed;
            }
            if( !f.Type.IsNullable )
            {
                monitor.Error( $"Ubiquitous value '{f.Type.CSharpName} {ownerType.CSharpName}.{f.Name}' must be nullable. Ubiquitous values must always be nullable." );
                return CSCodeGenerationResult.Failed;
            }
            crisTypeRegistry.RegisterUbiquitousValueDefinitionField( owner, f );
            return CSCodeGenerationResult.Success;
        }

    }
}
