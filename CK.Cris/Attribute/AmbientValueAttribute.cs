using CK.Core;
using System;

namespace CK.Cris
{
    /// <summary>
    /// Decorates a <see cref="IPoco"/> property that must be nullable: the property
    /// must be declared in <see cref="AmbientValues.IAmbientValues"/>.
    /// </summary>
    [AttributeUsage( AttributeTargets.Property )]
    public sealed class AmbientValueAttribute : Attribute
    {
    }
}
