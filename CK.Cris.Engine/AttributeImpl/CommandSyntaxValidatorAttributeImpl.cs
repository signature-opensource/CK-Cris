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

    sealed class CommandSyntaxValidatorAttributeImpl : BaseHandlerAttributeImpl
    {
        readonly CommandSyntaxValidatorAttribute _a;

        public CommandSyntaxValidatorAttributeImpl( CommandSyntaxValidatorAttribute a, Type t, MethodInfo m )
            : base( t, m )
        {
            _a = a;
        }

        private protected override CSCodeGenerationResult DoImplement( IActivityMonitor monitor, CrisTypeRegistry crisTypeRegistry, IStObjFinalClass impl, MethodInfo method )
        {
            return crisTypeRegistry.RegisterValidator( monitor, true, impl!, method, _a.FileName, _a.LineNumber )
                    ? CSCodeGenerationResult.Success
                    : CSCodeGenerationResult.Failed;
        }
    }
}
