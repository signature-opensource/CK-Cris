using CK.Core;
using System;
using System.Diagnostics;
using System.Reflection;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Base class for CommandValidatorAttributeImpl, CommandHandlerAttributeImpl, CommandPostHandlerAttributeImpl
    /// and RoutedEventHandlerAttributeImpl.
    /// </summary>
    abstract class BaseHandlerAttributeImpl
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
                Debug.Assert( "AttributeImpl".Length == 13 );
                return n.Remove( n.Length - 13 );
            }
        }

        protected (CrisRegistry? Registry, IStObjFinalClass? Impl, MethodInfo Method) Prepare( IActivityMonitor monitor,
                                                                                               ICSCodeGenerationContext codeGenContext )
        {
            IStObjFinalClass? impl = codeGenContext.CurrentRun.EngineMap.ToLeaf( _type );
            if( !_method.IsPublic )
            {
                monitor.Error( $"Method '{_type.FullName}.{_method.Name}' that is a [{AttributeName}] must be public." );
            }
            else if( impl == null )
            {
                monitor.Error( $"Unable to find a mapping for '{_type.FullName}': attribute [{AttributeName}] on method {_method.Name} cannot be used." );
            }
            else
            {
                return (CrisRegistry.FindOrCreate( monitor, codeGenContext ), impl, _method);
            }
            return (null, null, _method);
        }
    }
}
