using CK.Setup;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Static class that triggers the implementation of the Cris commands (this extends the
    /// Poco TypeScript export).
    /// </summary>
    [ContextBoundDelegation( "CK.Setup.TypeScriptCrisCommandGeneratorImpl, CK.Cris.AspNet.Engine" )]
    public static class TypeScriptCrisCommandGenerator
    {
    }
}
