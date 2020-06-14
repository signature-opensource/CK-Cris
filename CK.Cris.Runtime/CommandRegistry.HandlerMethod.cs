using CK.Core;
using System;
using System.Reflection;

namespace CK.Setup.Cris
{
    public partial class CommandRegistry
    {
        public class HandlerMethod
        {
            public readonly Entry Command;
            public readonly IStObjFinalClass Owner;
            public readonly MethodInfo Method;
            public readonly ParameterInfo[] Parameters;
            public readonly ParameterInfo CommandParameter;
            public readonly bool IsRefAsync;
            public readonly bool IsValAsync;
            public readonly Type UnwrappedReturnType;

            public HandlerMethod(
                        Entry command,
                        IStObjFinalClass owner,
                        MethodInfo method,
                        ParameterInfo[] parameters,
                        ParameterInfo commandParameter )
            {
                Command = command;
                Owner = owner;
                Method = method;
                Parameters = parameters;
                CommandParameter = commandParameter;
                (UnwrappedReturnType, IsRefAsync, IsValAsync) = GetReturnParameterInfo( method );
            }
            public override string ToString() => $"{Owner.ClassType.FullName}.{Method.Name}";

        }

    }

}
