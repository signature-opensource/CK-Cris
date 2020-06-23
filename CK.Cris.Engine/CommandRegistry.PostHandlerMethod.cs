using CK.Core;
using System.Reflection;

namespace CK.Setup.Cris
{
    public partial class CommandRegistry
    {
        public class PostHandlerMethod
        {
            public readonly Entry Command;
            public readonly IStObjFinalClass Owner;
            public readonly MethodInfo Method;
            public readonly ParameterInfo[] Parameters;
            public readonly ParameterInfo CmdOrPartParameter;
            public readonly ParameterInfo? ResultParameter;
            public readonly bool IsRefAsync;
            public readonly bool IsValAsync;

            internal PostHandlerMethod(
                        Entry command,
                        IStObjFinalClass owner,
                        MethodInfo method,
                        ParameterInfo[] parameters,
                        ParameterInfo cmdOrPartParameter,
                        ParameterInfo? resultParameter,
                        bool isRefAsync,
                        bool isValAsync )
            {
                Command = command;
                Owner = owner;
                Method = method;
                Parameters = parameters;
                CmdOrPartParameter = cmdOrPartParameter;
                ResultParameter = resultParameter;
                IsRefAsync = isRefAsync;
                IsValAsync = isValAsync;
            }
        }

    }

}
