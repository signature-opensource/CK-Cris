using CK.Core;
using CK.Cris;
using System;
using System.Reflection;

namespace CK.Setup.Cris
{
    sealed class RoutedEventHandlerAttributeImpl : BaseHandlerAttributeImpl
    {
        readonly RoutedEventHandlerAttribute _a;

        public RoutedEventHandlerAttributeImpl( RoutedEventHandlerAttribute a, Type t, MethodInfo m )
            : base( t, m )
        {
            _a = a;
        }

        private protected override CSCodeGenerationResult DoImplement( IActivityMonitor monitor, CrisTypeRegistry crisTypeRegistry, IStObjFinalClass impl, MethodInfo method )
        {
            return crisTypeRegistry.RegisterRoutedEventHandler( monitor, impl!, method, _a.FileName, _a.LineNumber )
                    ? CSCodeGenerationResult.Success
                    : CSCodeGenerationResult.Failed;
        }
    }
}
