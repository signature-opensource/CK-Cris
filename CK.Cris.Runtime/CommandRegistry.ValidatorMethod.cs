using CK.Core;
using System.Reflection;

namespace CK.Setup.Cris
{
    public partial class CommandRegistry
    {
        public class ValidatorMethod
        {
            public readonly Entry Command;
            public readonly IStObjFinalClass Owner;
            public readonly MethodInfo Method;
            public readonly ParameterInfo[] Parameters;
            public readonly ParameterInfo CommandParameter;
            public readonly bool IsRefAsync;
            public readonly bool IsValAsync;

            internal ValidatorMethod(
                        Entry command,
                        IStObjFinalClass owner,
                        MethodInfo method,
                        ParameterInfo[] parameters,
                        ParameterInfo commandParameter,
                        bool isRefAsync,
                        bool isValAsync )
            {
                Command = command;
                Owner = owner;
                Method = method;
                Parameters = parameters;
                CommandParameter = commandParameter;
                IsRefAsync = isRefAsync;
                IsValAsync = isValAsync;
            }
        }

    }

}
