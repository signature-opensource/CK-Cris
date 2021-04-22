using CK.Core;
using System;

namespace CK.Cris
{
    /// <summary>
    /// Marker interface to define mixable command parts.
    /// </summary>
    /// <remarks>
    /// Parts can be composed: when defining a specialized part that extends an
    /// existing <see cref="ICommandPart"/>, the <see cref="CKTypeDefinerAttribute"/> must be
    /// applied to the specialized part.
    /// </remarks>
    [CKTypeSuperDefiner]
    public interface ICommandPart : ICommand
    {
    }

}
