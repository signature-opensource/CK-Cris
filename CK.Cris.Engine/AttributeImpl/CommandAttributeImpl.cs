using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Setup.Cris
{
    class CommandAttributeImpl
    {
        readonly Type _type;
        readonly MethodInfo _method;

        public CommandAttributeImpl( Type t, MethodInfo m )
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

        protected (CommandRegistry? Registry, IStObjFinalClass? Impl, MethodInfo Method) Prepare( IActivityMonitor monitor, ICSCodeGenerationContext codeGenContext )
        {
            IStObjFinalClass? impl = codeGenContext.CurrentRun.EngineMap.Find( _type );
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
                return (CommandRegistry.FindOrCreate( monitor, codeGenContext ), impl, _method);
            }
            return (null, null, _method);
        }
    }
}
