using CK.Core;
using System;

namespace CK.Cris
{
    /// <summary>
    /// A command handler is a <see cref="IAutoService"/>.
    /// Implementations must expose one or two public methods that handle the command:
    /// <para>
    /// - An HandleAsync method that returns a Task (or Task&lt;TResult&gt; if the command is a <see cref="ICommand{TResult}"/>)
    /// that accepts at least a <typeparamref name="TCommand"/> parameter: other parameters must be available scoped or singleton services.
    /// </para>
    /// <para>
    /// - A Handle (synchrounous) method that returns void (or TResult if the command is a <see cref="ICommand{TResult}"/>)
    /// that accepts at least a <typeparamref name="TCommand"/> parameter: other parameters must be available scoped or singleton services.
    /// </para>
    /// </summary>
    /// <typeparam name="TCommand">The command type that this handler handles.</typeparam>
    public interface ICommandHandler<TCommand> : IAutoService where TCommand : ICommand
    {
    }

}
