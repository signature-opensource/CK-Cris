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

        /// <summary>
        /// Gets or sets whether the <see cref="IAbstractCommand"/> that the method accepts doesn't
        /// need to be a unified interface of all the interfaces that define the <see cref="IAbstractCommand"/>.
        /// Defaults to false: the "closed interface requirement" is the rule!
        /// </summary>
        public bool AllowUnclosedCommand { get; set; }
    }
}
