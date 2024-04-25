using CK.Core;
using System;
using CK.Cris.EndpointValues;
using CK.Setup;

namespace CK.Cris
{
    /// <summary>
    /// Defines a <see cref="IPoco"/> property as an Endpoint values: its value is bound to one (or more) endpoint
    /// or ambient services.
    /// <list type="bullet">
    ///     <item>The property type must be nullable nut must be not null for the command to be valid.</item>
    ///     <item>
    ///     A property with the same name must be declared in an extension of <see cref="IEndpointValues"/> (a secondary
    ///     Poco that extends IEndpointValues).
    ///     </item>
    ///     <item>
    ///     A [CommandPostHandler] must update the <see cref="IEndpointValues"/> 
    ///     </item>
    ///     <item>
    ///     A [CommandEndpointValidator] method must validate the value against one or more endpoint services.
    ///     </item>
    /// </list>
    /// </summary>
    [AttributeUsage( AttributeTargets.Property )]
    public sealed class EndpointValueAttribute : ContextBoundDelegationAttribute, INullInvalidAttribute
    {
        public EndpointValueAttribute()
            : base( "CK.Setup.Cris.EndpointValueAttributeImpl, CK.Cris.Engine" )
        {
        }
    }
}
