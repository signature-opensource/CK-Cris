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
    sealed class CommandHandlerAttributeImpl : CommandAttributeImpl, ICSCodeGenerator
    {
        readonly CommandHandlerAttribute _a;

        public CommandHandlerAttributeImpl( CommandHandlerAttribute a, Type t, MethodInfo m )
            : base( t, m )
        {
            _a = a;
        }

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext codeGenContext )
        {
            var (registry, impl, method) = Prepare( monitor, codeGenContext );
            Debug.Assert( (registry == null) == (impl == null) );
            return registry != null && registry.RegisterHandler( monitor, impl!, method, _a.AllowUnclosedCommand )
                    ? CSCodeGenerationResult.Success
                    : CSCodeGenerationResult.Failed;
        }
    }
}
