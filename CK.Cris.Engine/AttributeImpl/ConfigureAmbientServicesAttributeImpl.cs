using CK.Core;
using CK.Cris;
using System;
using System.Reflection;

namespace CK.Setup.Cris
{
    sealed class ConfigureAmbientServicesAttributeImpl : BaseHandlerAttributeImpl
    {
        readonly ConfigureAmbientServicesAttribute _a;

        public ConfigureAmbientServicesAttributeImpl( ConfigureAmbientServicesAttribute a, Type t, MethodInfo m )
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
            return crisTypeRegistry.RegisterMultiTargetHandler( monitor, MultiTargetHandlerKind.ConfigureAmbientServices, engineMap, impl!, method, _a.FileName, _a.LineNumber )
                    ? CSCodeGenerationResult.Success
                    : CSCodeGenerationResult.Failed;
        }
    }
}
