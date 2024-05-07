using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Applies to <see cref="CrisHandlerKind.CommandIncomingValidator"/>, <see cref="CrisHandlerKind.CommandHandlingValidator"/>
    /// and <see cref="CrisHandlerKind.ConfigureServices"/>: void async or not method with a parameter with the command, event or part
    /// and an expected parameter (<see cref="UserMessageCollector"/> for validators, <see cref="AmbientServiceHub"/> for configure).
    /// Other parameters are resolved from a IServiceProvider.
    /// </summary>
    public sealed class HandlerMultiTargetMethod : HandlerBase
    {
        public override CrisHandlerKind Kind => _kind;
        public readonly ParameterInfo ThisPocoParameter;
        public readonly ParameterInfo ArgumentParameter;
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
                                         ParameterInfo argumentParameter,
                                         bool isRefAsync,
                                         bool isValAsync )
            : base( crisType, owner, method, parameters, fileName, lineNumber )
        {
            _kind = kind;
            ThisPocoParameter = thisPocoParameter;
            ArgumentParameter = argumentParameter;
            IsRefAsync = isRefAsync;
            IsValAsync = isValAsync;
        }
    }

}
