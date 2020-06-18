using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Cris
{
    /// <summary>
    /// Decorates a method that is a command (or command part) post handler.
    /// </summary>
    [AttributeUsage( AttributeTargets.Method, AllowMultiple = false, Inherited = false )]
    public class CommandPostHandlerAttribute : ContextBoundDelegationAttribute
    {
        /// <summary>
        /// Initializes a new <see cref="CommandPostHandlerAttribute"/>.
        /// </summary>
        public CommandPostHandlerAttribute()
            : base( "CK.Setup.Cris.CommandPostHandlerAttributeImpl, CK.Cris.Runtime" )
        {
        }
    }
}
