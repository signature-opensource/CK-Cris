using CK.Core;
using CK.Cris;
using System;
using System.Reflection;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Captures command handler method information.
    /// </summary>
    public sealed class HandlerMethod : HandlerBase
    {
        /// <summary>
        /// Always <see cref="CrisHandlerKind.CommandHandler"/>.
        /// </summary>
        public override CrisHandlerKind Kind => CrisHandlerKind.CommandHandler;

        /// <summary>
        /// The parameter that is the command.
        /// </summary>
        public readonly ParameterInfo CommandParameter;

        /// <summary>
        /// The returned type.
        /// </summary>
        public readonly Type UnwrappedReturnType;

        /// <summary>
        /// Whether this handler handles the IPoco's closure.
        /// </summary>
        public readonly bool IsClosedHandler;

        internal HandlerMethod( CrisType crisType,
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
            : base( crisType, owner, method, parameters, fileName, lineNumber, isRefAsync, isValAsync )
        {
            CommandParameter = commandParameter;
            UnwrappedReturnType = unwrappedReturnType;
            IsClosedHandler = isClosedHandler;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Owner.ClassType.FullName}.{Method.Name}";

    }

}
