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
    sealed class CommandPostHandlerAttributeImpl : CommandAttributeImpl, ICSCodeGenerator
    {
        public CommandPostHandlerAttributeImpl( CommandPostHandlerAttribute a, Type t, MethodInfo m )
            : base( t, m )
        {
        }

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext codeGenContext )
        {
            var (registry, impl, method) = Prepare( monitor, codeGenContext );
            Debug.Assert( (registry == null) == (impl == null) );
            return registry != null && registry.RegisterPostHandler( monitor, impl!, method )
                    ? CSCodeGenerationResult.Success
                    : CSCodeGenerationResult.Failed;
        }
    }
}
