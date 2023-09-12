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

    sealed class CommandValidatorAttributeImpl : BaseHandlerAttributeImpl, ICSCodeGenerator
    {
        readonly CommandValidatorAttribute _a;

        public CommandValidatorAttributeImpl( CommandValidatorAttribute a, Type t, MethodInfo m )
            : base( t, m )
        {
            _a = a;
        }

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext codeGenContext )
        {
            var (registry, impl, method) = Prepare( monitor, codeGenContext );
            Throw.DebugAssert( registry == null || impl != null, "registry available => final implementation of the class that implements the method exists." );
            return registry != null && registry.RegisterValidator( monitor, impl!, method, _a.FileName, _a.LineNumber )
                    ? CSCodeGenerationResult.Success
                    : CSCodeGenerationResult.Failed;
        }
    }
}
