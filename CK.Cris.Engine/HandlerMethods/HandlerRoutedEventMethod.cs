using CK.Core;
using CK.Cris;
using System.Reflection;

namespace CK.Setup.Cris
{
    public sealed class HandlerRoutedEventMethod : HandlerBase
    {
        public override CrisHandlerKind Kind => CrisHandlerKind.RoutedEventHandler;
        public readonly ParameterInfo EventOrPartParameter;
        public readonly bool IsRefAsync;
        public readonly bool IsValAsync;

        internal HandlerRoutedEventMethod( CrisType crisType,
                                           IStObjFinalClass owner,
                                           MethodInfo method,
                                           ParameterInfo[] parameters,
                                           string? fileName,
                                           int lineNumber,
                                           ParameterInfo eventOrPartParameter,
                                           bool isRefAsync,
                                           bool isValAsync )
            : base( crisType, owner, method, parameters, fileName, lineNumber )
        {
            EventOrPartParameter = eventOrPartParameter;
            IsRefAsync = isRefAsync;
            IsValAsync = isValAsync;
        }
    }

}
