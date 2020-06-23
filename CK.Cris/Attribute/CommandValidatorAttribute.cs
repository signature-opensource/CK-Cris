using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Cris
{
    /// <summary>
    /// Decorates a method that is a command or command part validator.
    /// </summary>
    [AttributeUsage( AttributeTargets.Method, AllowMultiple = false, Inherited = false )]
    public class CommandValidatorAttribute : ContextBoundDelegationAttribute
    {
        /// <summary>
        /// Initializes a new <see cref="CommandValidatorAttribute"/>.
        /// </summary>
        public CommandValidatorAttribute()
            : base( "CK.Setup.Cris.CommandValidatorAttributeImpl, CK.Cris.Engine" )
        {
        }
    }
}
