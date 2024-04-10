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
    sealed class CommandPostHandlerAttributeImpl : BaseHandlerAttributeImpl
    {
        readonly CommandPostHandlerAttribute _a;

        public CommandPostHandlerAttributeImpl( CommandPostHandlerAttribute a, Type t, MethodInfo m )
            : base( t, m )
        {
            _a = a;
        }

        private protected override CSCodeGenerationResult DoImplement( IActivityMonitor monitor, CrisTypeRegistry crisTypeRegistry, IStObjFinalClass impl, MethodInfo method )
        {
            return crisTypeRegistry.RegisterPostHandler( monitor, impl!, method, _a.FileName, _a.LineNumber )
                    ? CSCodeGenerationResult.Success
                    : CSCodeGenerationResult.Failed;
        }
    }
}
