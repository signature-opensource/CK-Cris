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
    /// </summary>
    /// <typeparam name="T">The command type.</typeparam>
    public interface ICommandHandler<in T> : IAutoService where T : ICommand
    {
    }
}
