using CK.Core;
using System;
using CK.Cris.AmbientValues;
using CK.Setup;

namespace CK.Cris
{
    /// <summary>
    /// Defines a <see cref="IPoco"/> property as a safe ambient values: its value is bound to one (or more) endpoint
    /// or ambient services.
    /// <list type="bullet">
    ///     <item>The property must be nullable.</item>
    ///     <item>
    ///     A property with the same name must be declared in an extension of <see cref="IAmbientValues"/> (a secondary
    ///     Poco that extends IAmbientValues).
    ///     </item>
    ///     <item>
    ///     A [CommandPostHandler] must update the <see cref="IAmbientValues"/> 
    ///     </item>
    ///     <item>
    ///     A [CommandSyntaxValidator] method must validate the value against one or more endpoint services.
    ///     </item>
    /// </list>
    /// </summary>
    [AttributeUsage( AttributeTargets.Property )]
    public sealed class SafeAmbientValueAttribute : ContextBoundDelegationAttribute
    {
        public SafeAmbientValueAttribute()
            : base( "CK.Setup.Cris.SafeAmbientValueAttributeImpl, CK.Cris.Engine" )
        {
        }
    }
}
