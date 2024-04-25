using CK.Core;
using System;
using CK.Cris.UbiquitousValues;
using CK.Setup;

namespace CK.Cris
{
    /// <summary>
    /// Defines a <see cref="IPoco"/> property as an ubiquitous value: the value is provided by ambient
    /// or processwide services.
    /// <list type="bullet">
    ///     <item>The property type must be nullable **but** must be not null for the command to be valid.</item>
    ///     <item>
    ///     A property with the same name must be declared in an extension of <see cref="IUbiquitousValues"/> (a secondary
    ///     Poco that extends IUbiquitousValues).
    ///     </item>
    ///     <item>
    ///     A [CommandPostHandler] must update the <see cref="IUbiquitousValues"/> 
    ///     </item>
    /// </list>
    /// Whether a [CommandEndpointValidator] method exists that validates the value against one or more services
    /// or a [ConfigureAmbientServices] method exists that configures the Ambient services from it, or the value
    /// is used as-is depends on the semantics of the value.
    /// </summary>
    [AttributeUsage( AttributeTargets.Property )]
    public sealed class UbiquitousValueAttribute : ContextBoundDelegationAttribute, INullInvalidAttribute
    {
        public UbiquitousValueAttribute()
            : base( "CK.Setup.Cris.UbiquitousValueAttributeImpl, CK.Cris.Engine" )
        {
        }
    }
}
