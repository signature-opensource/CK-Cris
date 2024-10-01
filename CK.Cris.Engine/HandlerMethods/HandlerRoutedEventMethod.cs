using CK.Core;
using CK.Cris;
using System.Reflection;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Captures a routed event handler method information.
    /// </summary>
    public sealed class HandlerRoutedEventMethod : HandlerBase
    {
        /// <summary>
        /// Always <see cref="CrisHandlerKind.RoutedEventHandler"/>.
        /// </summary>
        public override CrisHandlerKind Kind => CrisHandlerKind.RoutedEventHandler;

        /// <summary>
        /// The parameter that is the event or event part.
        /// </summary>
        public readonly ParameterInfo EventOrPartParameter;

        internal HandlerRoutedEventMethod( CrisType crisType,
                                           IStObjFinalClass owner,
                                           MethodInfo method,
                                           ParameterInfo[] parameters,
                                           string? fileName,
                                           int lineNumber,
                                           ParameterInfo eventOrPartParameter,
                                           bool isRefAsync,
                                           bool isValAsync )
            : base( crisType, owner, method, parameters, fileName, lineNumber, isRefAsync, isValAsync )
        {
            EventOrPartParameter = eventOrPartParameter;
        }
    }

}
