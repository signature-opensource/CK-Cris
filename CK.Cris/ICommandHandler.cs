using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Defines the fact that the class that ultimately implements this interface
    /// must handle the <typeparamref name="T"/> command: a [<see cref="CommandHandlerAttribute"/>] method
    /// must exist that implement it.
    /// </summary>
    /// <typeparam name="T">The command type.</typeparam>
    public interface ICommandHandler<in T> : IAutoService where T : ICommand
    {
    }
}
