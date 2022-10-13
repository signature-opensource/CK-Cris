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
    sealed class CommandValidatorAttributeImpl : CommandAttributeImpl, ICSCodeGenerator
    {
        public CommandValidatorAttributeImpl( CommandValidatorAttribute a, Type t, MethodInfo m )
            : base( t, m )
        {
        }

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext codeGenContext )
        {
            var (registry, impl, method) = Prepare( monitor, codeGenContext );
            Debug.Assert( registry == null || impl != null, "registry available => final implementation of the class that implements the method exists." );
            return registry != null && registry.RegisterValidator( monitor, impl!, method )
                    ? CSCodeGenerationResult.Success
                    : CSCodeGenerationResult.Failed;
        }
    }
}
