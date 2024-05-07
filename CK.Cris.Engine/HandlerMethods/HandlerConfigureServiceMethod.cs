using CK.Core;
using CK.Cris;
using System.Reflection;

namespace CK.Setup.Cris
{
    public sealed class HandlerConfigureServiceMethod : HandlerBase
    {
        public override CrisHandlerKind Kind => CrisHandlerKind.ConfigureServices;
        public readonly ParameterInfo CmdOrPartParameter;
        public readonly ParameterInfo AmbientServiceHubParameter;
        public readonly bool IsRefAsync;
        public readonly bool IsValAsync;

        internal HandlerConfigureServiceMethod( CrisType crisType,
                                                IStObjFinalClass owner,
                                                MethodInfo method,
                                                ParameterInfo[] parameters,
                                                string? fileName,
                                                int lineNumber,
                                                ParameterInfo cmdOrPartParameter,
                                                ParameterInfo ambientServiceHubParameter,
                                                bool isRefAsync,
                                                bool isValAsync )
            : base( crisType, owner, method, parameters, fileName, lineNumber )
        {
            CmdOrPartParameter = cmdOrPartParameter;
            AmbientServiceHubParameter = ambientServiceHubParameter;
            IsRefAsync = isRefAsync;
            IsValAsync = isValAsync;
        }
    }

}
