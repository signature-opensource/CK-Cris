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
    class CommandValidatorAttributeImpl : CommandAttributeImpl, ICodeGenerator
    {
        public CommandValidatorAttributeImpl( CommandValidatorAttribute a, Type t, MethodInfo m )
            : base( t, m )
        {
        }

        public AutoImplementationResult Implement( IActivityMonitor monitor, ICodeGenerationContext codeGenContext )
        {
            var (registry, impl, method) = Prepare( monitor, codeGenContext );
            Debug.Assert( (registry == null) == (impl == null) );
            return registry != null && registry.RegisterValidator( monitor, impl!, method )
                    ? AutoImplementationResult.Success
                    : AutoImplementationResult.Failed;
        }
    }
}
