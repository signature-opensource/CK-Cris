using CK.Core;
using CK.Cris;
using System;
using System.Reflection;

namespace CK.Setup.Cris
{

    sealed class CommandEndpointValidatorAttributeImpl : BaseHandlerAttributeImpl
    {
        readonly CommandEnspointValidatorAttribute _a;

        public CommandEndpointValidatorAttributeImpl( CommandEnspointValidatorAttribute a, Type t, MethodInfo m )
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
