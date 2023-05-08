using CK.Core;
using CK.Cris;
using System;
using System.Diagnostics;
using System.Reflection;

namespace CK.Setup.Cris
{
    sealed class RoutedEventHandlerAttributeImpl : BaseHandlerAttributeImpl, ICSCodeGenerator
    {
        readonly RoutedEventHandlerAttribute _a;

        public RoutedEventHandlerAttributeImpl( RoutedEventHandlerAttribute a, Type t, MethodInfo m )
            : base( t, m )
        {
            _a = a;
        }

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext codeGenContext )
        {
            var (registry, impl, method) = Prepare( monitor, codeGenContext );
            Debug.Assert( registry == null || impl != null, "registry available => final implementation of the class that implements the method exists." );
            return registry != null && registry.RegisterRoutedEvent( monitor, impl!, method, _a.FileName, _a.LineNumber )
                    ? CSCodeGenerationResult.Success
                    : CSCodeGenerationResult.Failed;
        }
    }
}
