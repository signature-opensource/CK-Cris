using CK.Core;
using CK.Cris;
using System.Reflection;

namespace CK.Setup.Cris
{
    public partial class CrisRegistry
    {
        public sealed class RoutedEventHandlerMethod : BaseHandler
        {
            public override CrisHandlerKind Kind => CrisHandlerKind.RoutedEventHandler;
            public readonly ParameterInfo EventOrPartParameter;
            public readonly bool IsRefAsync;
            public readonly bool IsValAsync;

            internal RoutedEventHandlerMethod( Entry command,
                                               IStObjFinalClass owner,
                                               MethodInfo method,
                                               ParameterInfo[] parameters,
                                               string? fileName,
                                               int lineNumber,
                                               ParameterInfo eventOrPartParameter,
                                               bool isRefAsync,
                                               bool isValAsync )
                : base( command, owner, method, parameters, fileName, lineNumber )
            {
                EventOrPartParameter = eventOrPartParameter;
                IsRefAsync = isRefAsync;
                IsValAsync = isValAsync;
            }
        }

    }

}
