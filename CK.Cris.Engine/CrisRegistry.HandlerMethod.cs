using CK.Core;
using CK.Cris;
using System;
using System.Reflection;

namespace CK.Setup.Cris
{
    public partial class CrisRegistry
    {
        public sealed class HandlerMethod : BaseHandler
        {
            public override CrisHandlerKind Kind => CrisHandlerKind.CommandHandler;
            public readonly ParameterInfo CommandParameter;
            public readonly Type UnwrappedReturnType;
            public readonly bool IsRefAsync;
            public readonly bool IsValAsync;
            public readonly bool IsClosedHandler;

            public HandlerMethod( Entry command,
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
                : base( command, owner, method, parameters, fileName, lineNumber )
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

}
