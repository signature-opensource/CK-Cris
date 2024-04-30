using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup.Cris
{
    public sealed class HandlerValidatorMethod : HandlerBase
    {
        public override CrisHandlerKind Kind => _kind;
        public readonly ParameterInfo CmdOrPartParameter;
        public readonly ParameterInfo ValidationContextParameter;
        public readonly bool IsRefAsync;
        public readonly bool IsValAsync;
        readonly CrisHandlerKind _kind;

        internal HandlerValidatorMethod( CrisType crisType,
                                         CrisHandlerKind kind,
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
            _kind = kind;
            CmdOrPartParameter = cmdOrPartParameter;
            ValidationContextParameter = validationContextParameter;
            IsRefAsync = isRefAsync;
            IsValAsync = isValAsync;
        }
    }

}
