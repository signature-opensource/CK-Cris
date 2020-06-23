using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Cris
{
    /// <summary>
    /// Decorates a method that is a command handler.
    /// </summary>
    [AttributeUsage( AttributeTargets.Method, AllowMultiple = false, Inherited = false )]
    public class CommandHandlerAttribute : ContextBoundDelegationAttribute
    {
        /// <summary>
        /// Initializes a new <see cref="CommandHandlerAttribute"/>.
        /// </summary>
        public CommandHandlerAttribute()
            : base( "CK.Setup.Cris.CommandHandlerAttributeImpl, CK.Cris.Engine" )
        {
        }
    }
}
