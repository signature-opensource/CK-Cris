using CK.Core;
using System;

namespace CK.Cris
{
    /// <summary>
    /// Marker interface to define mixable command parts.
    /// <para>
    /// Since command parts de facto defines a command object, their name should start 
    /// with "ICommand" in order to distinguish them from actual commands that
    /// should be suffixed with "Command".
    /// </para>
    /// </summary>
    /// <remarks>
    /// Parts can also be extended: when defining a specialized part that extends an
    /// existing <see cref="ICommandPart"/>, the <see cref="CKTypeDefinerAttribute"/> must be
    /// applied to the specialized part.
    /// </remarks>
    [CKTypeSuperDefiner]
    public interface ICommandPart : ICrisPoco
    {
    }

}
