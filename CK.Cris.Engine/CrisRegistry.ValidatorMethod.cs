using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup.Cris
{
    public partial class CrisRegistry
    {

        public sealed class ValidatorMethod : BaseHandler
        {
            public override CrisHandlerKind Kind => CrisHandlerKind.CommandValidator;
            public readonly ParameterInfo CmdOrPartParameter;
            public readonly bool IsRefAsync;
            public readonly bool IsValAsync;

            internal ValidatorMethod( Entry command,
                                      IStObjFinalClass owner,
                                      MethodInfo method,
                                      ParameterInfo[] parameters,
                                      string? fileName,
                                      int lineNumber,
                                      ParameterInfo cmdOrPartParameter,
                                      bool isRefAsync,
                                      bool isValAsync )
                : base( command, owner, method, parameters, fileName, lineNumber )
            {
                CmdOrPartParameter = cmdOrPartParameter;
                IsRefAsync = isRefAsync;
                IsValAsync = isValAsync;
            }
        }
    }
}
