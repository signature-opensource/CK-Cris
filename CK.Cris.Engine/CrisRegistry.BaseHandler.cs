using CK.Core;
using CK.Cris;
using System.Reflection;

namespace CK.Setup.Cris
{
    public partial class CrisRegistry
    {
        /// <summary>
        /// Generalizes the 4 kind of handler methods.
        /// Engine implementation of <see cref="ICrisPocoModel.IHandler"/>.
        /// </summary>
        public abstract class BaseHandler
        {
            public readonly Entry Command;
            public readonly IStObjFinalClass Owner;
            public readonly MethodInfo Method;
            public readonly ParameterInfo[] Parameters;

            public abstract CrisHandlerKind Kind { get; }

            protected BaseHandler( Entry command, IStObjFinalClass owner, MethodInfo method, ParameterInfo[] parameters )
            {
                Command = command;
                Owner = owner;
                Method = method;
                Parameters = parameters;
            }
        }

    }

}
