using CK.Core;
using System;

namespace CK.Cris
{
    /// <summary>
    /// Marker interface to define mixable command parts.
    /// </summary>
    [CKTypeSuperDefiner]
    public interface ICommandPart : IPoco
    {
    }

}
