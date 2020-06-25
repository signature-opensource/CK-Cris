using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Attribute marker for parameter attribute that states that a command
    /// handler allows its command to not be the "unified", "closed", interface.
    /// <para>
    /// This interface is like <see cref="IAutoService"/> and its friends: only its name matter
    /// and it can be locally defined in any asemblies that need it.
    /// </para>
    /// </summary>
    public interface IAllowUnclosedCommandAttribute
    {
    }
}
