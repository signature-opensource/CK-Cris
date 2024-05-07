using CK.Core;
using CK.Cris;
using System;
using System.Reflection;

namespace CK.Setup.Cris
{
    sealed class CommandIncomingValidatorAttributeImpl : BaseHandlerAttributeImpl
    {
        readonly CommandIncomingValidatorAttribute _a;

        public CommandIncomingValidatorAttributeImpl( CommandIncomingValidatorAttribute a, Type t, MethodInfo m )
            : base( t, m )
        {
            _a = a;
        }

        private protected override CSCodeGenerationResult DoImplement( IActivityMonitor monitor,
                                                                       IStObjMap engineMap,
                                                                       CrisTypeRegistry crisTypeRegistry,
                                                                       IStObjFinalClass impl,
                                                                       MethodInfo method )
        {
            return crisTypeRegistry.RegisterMultiTargetHandler( monitor, MultiTargetHandlerKind.CommandIncomingValidator, engineMap, impl!, method, _a.FileName, _a.LineNumber )
                    ? CSCodeGenerationResult.Success
                    : CSCodeGenerationResult.Failed;
        }
    }
}
