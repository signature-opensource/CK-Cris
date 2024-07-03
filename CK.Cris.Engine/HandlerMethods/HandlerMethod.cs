using CK.Core;
using CK.Cris;
using System;
using System.Reflection;

namespace CK.Setup.Cris
{
    public sealed class HandlerMethod : HandlerBase
    {
        public override CrisHandlerKind Kind => CrisHandlerKind.CommandHandler;
        public readonly ParameterInfo CommandParameter;
        public readonly Type UnwrappedReturnType;
        public readonly bool IsRefAsync;
        public readonly bool IsValAsync;
        public readonly bool IsClosedHandler;

        public HandlerMethod( CrisType crisType,
                              IStObjFinalClass owner,
                              MethodInfo method,
                              ParameterInfo[] parameters,
                              string? fileName,
                              int lineNumber,
                              ParameterInfo commandParameter,
                              Type unwrappedReturnType,
                              bool isRefAsync,
                              bool isValAsync,
                              bool isClosedHandler )
            : base( crisType, owner, method, parameters, fileName, lineNumber )
        {
            CommandParameter = commandParameter;
            UnwrappedReturnType = unwrappedReturnType;
            IsRefAsync = isRefAsync;
            IsValAsync = isValAsync;
            IsClosedHandler = isClosedHandler;
        }

        public override string ToString() => $"{Owner.ClassType.FullName}.{Method.Name}";

    }

}
