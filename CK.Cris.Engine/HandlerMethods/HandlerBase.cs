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
        /// <summary>
        /// The Cris type that is handled.
        /// </summary>
        public readonly CrisType CrisType;

        /// <summary>
        /// The generated class that holds the handler.
        /// </summary>
        public readonly IStObjFinalClass Owner;

        /// <summary>
        /// The handler method information.
        /// </summary>
        public readonly MethodInfo Method;

        /// <summary>
        /// The method parameters.
        /// </summary>
        public readonly ParameterInfo[] Parameters;

        /// <summary>
        /// The file name that defines the handler.
        /// </summary>
        public readonly string FileName;

        /// <summary>
        /// The line number at which the handler is defined in the <see cref="FileName"/>.
        /// </summary>
        public readonly int LineNumber;

        /// <summary>
        /// Whether the handler is a regular asyncronous (Task) method.
        /// </summary>
        public readonly bool IsRefAsync;

        /// <summary>
        /// Whether the handler is a ValueTask asyncronous method.
        /// </summary>
        public readonly bool IsValAsync;

        /// <summary>
        /// The kind of handler.
        /// </summary>
        public abstract CrisHandlerKind Kind { get; }

        private protected HandlerBase( CrisType t,
                                       IStObjFinalClass owner,
                                       MethodInfo method,
                                       ParameterInfo[] parameters,
                                       string? fileName,
                                       int lineNumber,
                                       bool isRefAsync,
                                       bool isValAsync )
        {
            CrisType = t;
            Owner = owner;
            Method = method;
            Parameters = parameters;
            FileName = fileName ?? string.Empty;
            LineNumber = lineNumber;
            IsRefAsync = isRefAsync;
            IsValAsync = isValAsync;
        }
    }

}
