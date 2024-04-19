using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup.Cris
{
    public sealed class HandlerValidatorMethod : HandlerBase
    {
        readonly bool _isSyntax;
        public override CrisHandlerKind Kind => _isSyntax ? CrisHandlerKind.CommandSyntaxValidator : CrisHandlerKind.CommandValidator;
        public readonly ParameterInfo CmdOrPartParameter;
        public readonly ParameterInfo ValidationContextParameter;
        public readonly bool IsRefAsync;
        public readonly bool IsValAsync;

        internal HandlerValidatorMethod( CrisType crisType,
                                         bool isSyntax,
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
            _isSyntax = isSyntax;
            CmdOrPartParameter = cmdOrPartParameter;
            ValidationContextParameter = validationContextParameter;
            IsRefAsync = isRefAsync;
            IsValAsync = isValAsync;
        }
    }

}
