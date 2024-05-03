using CK.Core;
using System;
using CK.Cris.AmbientValues;
using CK.Setup;

namespace CK.Cris
{
    /// <summary>
    /// Defines a <see cref="IPoco"/> property as an ambient value: the value is bound to an ambient service.
    /// <list type="bullet">
    ///     <item>The property type must be nullable **but** must be not null for the command to be valid.</item>
    ///     <item>
    ///     A property with the same name (and same type but non nullable) must be declared in an extension of <see cref="IAmbientValues"/>
    ///     (a secondary Poco that extends IAmbientValues).
    ///     </item>
    ///     <item>
    ///     A [CommandPostHandler] must update the <see cref="IAmbientValues"/> from the Ambient services.
    ///     </item>
    /// </list>
    /// There can be [CommandIncomingValidator] methods can exist that validate the value against one or more services.
    /// <para>
    /// When <see cref="IsSafe"/> is false, a [ConfigureAmbientServices] method must exist and there can
    /// be [CommandIncomingValidator] methods that syntaxically validates the command, event or part that defines the property.
    /// </para>
    /// <para>
    /// When <see cref="IsSafe"/> is true, there must be at least one [CommandIncomingValidator] method that validates the command
    /// against an ambient service and no [ConfigureAmbientServices] method.
    /// </para>
    /// </summary>
    [AttributeUsage( AttributeTargets.Property )]
    public sealed class AmbientServiceValueAttribute : ContextBoundDelegationAttribute, INullInvalidAttribute
    {
        /// <summary>
        /// Initializes a new <see cref="AmbientValuesService"/>.
        /// </summary>
        /// <param name="isSafe">Whether this is a safe ambient service.</param>
        public AmbientServiceValueAttribute( bool isSafe = false )
            : base( "CK.Setup.Cris.AmbientServiceValueAttributeImpl, CK.Cris.Engine" )
        {
            IsSafe = isSafe;
        }

        /// <summary>
        /// Gets whether this ambient value is checked against one or more ambient services or
        /// this value configures the ambient services.
        /// </summary>
        public bool IsSafe { get; }
    }
}
