using CK.Core;
using CK.Cris;
using System;
using System.Reflection;

namespace CK.Setup.Cris
{

    /// <summary>
    /// Applies to <see cref="CrisHandlerKind.IncomingValidator"/>, <see cref="CrisHandlerKind.CommandHandlingValidator"/>,
    /// <see cref="CrisHandlerKind.RoutedEventHandler"/>, <see cref="CrisHandlerKind.ConfigureAmbientServices"/> and
    /// <see cref="CrisHandlerKind.RestoreAmbientServices"/>.
    /// <para>
    /// This is a void async or not method with a parameter that is the command, event or part
    /// and an expected argument (<see cref="UserMessageCollector"/> for validators, <see cref="AmbientServiceHub"/> for configure)
    /// or no argument for RestoreAmbientServices and RoutedEventHandler.
    /// </para>
    /// Other parameters are resolved from a IServiceProvider.
    /// </summary>
    public sealed class HandlerMultiTargetMethod : HandlerBase
    {
        public override CrisHandlerKind Kind => _kind;
        public readonly ParameterInfo ThisPocoParameter;
        public readonly ParameterInfo? ArgumentParameter;
        public readonly bool IsRefAsync;
        public readonly bool IsValAsync;
        readonly CrisHandlerKind _kind;

        internal HandlerMultiTargetMethod( CrisType crisType,
                                           CrisHandlerKind kind,
                                           IStObjFinalClass owner,
                                           MethodInfo method,
                                           ParameterInfo[] parameters,
                                           string? fileName,
                                           int lineNumber,
                                           ParameterInfo thisPocoParameter,
                                           ParameterInfo? argumentParameter,
                                           bool isRefAsync,
                                           bool isValAsync )
            : base( crisType, owner, method, parameters, fileName, lineNumber )
        {
            Throw.DebugAssert( argumentParameter != null );
            _kind = kind;
            ThisPocoParameter = thisPocoParameter;
            ArgumentParameter = argumentParameter;
            IsRefAsync = isRefAsync;
            IsValAsync = isValAsync;
        }
    }

}
