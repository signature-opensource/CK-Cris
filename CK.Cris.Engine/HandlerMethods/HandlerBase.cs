using CK.Core;
using CK.Cris;
using System.Reflection;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Generalizes the 5 kind of handler methods.
    /// Engine implementation of <see cref="ICrisPocoModel.IHandler"/>.
    /// </summary>
    public abstract class HandlerBase
    {
        public readonly CrisType CrisType;
        public readonly IStObjFinalClass Owner;
        public readonly MethodInfo Method;
        public readonly ParameterInfo[] Parameters;
        public readonly string FileName;
        public readonly int LineNumber;

        public abstract CrisHandlerKind Kind { get; }

        protected HandlerBase( CrisType t, IStObjFinalClass owner, MethodInfo method, ParameterInfo[] parameters, string? fileName, int lineNumber )
        {
            CrisType = t;
            Owner = owner;
            Method = method;
            Parameters = parameters;
            FileName = fileName ?? string.Empty;
            LineNumber = lineNumber;
        }
    }

}
