using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Enables a class that ultimately implements this interface to claim that 
    /// it handles the <typeparamref name="T"/> command: a method marked with [<see cref="CommandHandlerAttribute"/>] 
    /// must exist.
    /// <para>
    /// This interface is not required: when used it enables its command handler method to benefit of the <see cref="IAutoService"/>
    /// substitutability.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The command type.</typeparam>
    public interface ICommandHandler<in T> : IAutoService where T : ICrisPoco
    {
    }
}
