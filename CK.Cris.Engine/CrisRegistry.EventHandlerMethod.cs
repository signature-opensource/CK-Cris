using CK.Core;
using CK.Cris;
using System.Reflection;

namespace CK.Setup.Cris
{
    public partial class CrisRegistry
    {
        public sealed class EventHandlerMethod : BaseHandler
        {
            public override CrisHandlerKind Kind => CrisHandlerKind.RoutedEventHandler;
            public readonly ParameterInfo CmdOrPartParameter;
            public readonly bool IsRefAsync;
            public readonly bool IsValAsync;

            internal EventHandlerMethod( Entry command,
                                         IStObjFinalClass owner,
                                         MethodInfo method,
                                         ParameterInfo[] parameters,
                                         ParameterInfo cmdOrPartParameter,
                                         bool isRefAsync,
                                         bool isValAsync )
                : base( command, owner, method, parameters )
            {
                CmdOrPartParameter = cmdOrPartParameter;
                IsRefAsync = isRefAsync;
                IsValAsync = isValAsync;
            }

        }

    }

}
