using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup.Cris
{
    public sealed class HandlerValidatorMethod : HandlerBase
    {
        readonly bool _isIncoming;
        public override CrisHandlerKind Kind => _isIncoming ? CrisHandlerKind.CommandIncomingValidator : CrisHandlerKind.CommandHandlingValidator;
        public readonly ParameterInfo CmdOrPartParameter;
        public readonly ParameterInfo ValidationContextParameter;
        public readonly bool IsRefAsync;
        public readonly bool IsValAsync;

        internal HandlerValidatorMethod( CrisType crisType,
                                         bool isIncoming,
                                         IStObjFinalClass owner,
                                         MethodInfo method,
                                         ParameterInfo[] parameters,
                                         string? fileName,
                                         int lineNumber,
                                         ParameterInfo cmdOrPartParameter,
                                         ParameterInfo validationContextParameter,
                                         bool isRefAsync,
                                         bool isValAsync )
            : base( crisType, owner, method, parameters, fileName, lineNumber )
        {
            _isIncoming = isIncoming;
            CmdOrPartParameter = cmdOrPartParameter;
            ValidationContextParameter = validationContextParameter;
            IsRefAsync = isRefAsync;
            IsValAsync = isValAsync;
        }
    }

}
