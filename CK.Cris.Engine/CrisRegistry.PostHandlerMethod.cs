using CK.Core;
using CK.Cris;
using System.Reflection;

namespace CK.Setup.Cris
{
    public partial class CrisRegistry
    {
        public sealed class PostHandlerMethod : BaseHandler
        {
            public override CrisHandlerKind Kind => CrisHandlerKind.CommandPostHandler;
            public readonly ParameterInfo CmdOrPartParameter;
            public readonly ParameterInfo? ResultParameter;
            public readonly bool MustCastResultParameter;
            public readonly bool IsRefAsync;
            public readonly bool IsValAsync;

            internal PostHandlerMethod( Entry command,
                                        IStObjFinalClass owner,
                                        MethodInfo method,
                                        ParameterInfo[] parameters,
                                        string? fileName,
                                        int lineNumber,
                                        ParameterInfo cmdOrPartParameter,
                                        ParameterInfo? resultParameter,
                                        bool mustCastResultParameter,
                                        bool isRefAsync,
                                        bool isValAsync )
                : base( command, owner, method, parameters, fileName, lineNumber )
            {
                CmdOrPartParameter = cmdOrPartParameter;
                ResultParameter = resultParameter;
                MustCastResultParameter = mustCastResultParameter;
                IsRefAsync = isRefAsync;
                IsValAsync = isValAsync;
            }
        }
    }
}
