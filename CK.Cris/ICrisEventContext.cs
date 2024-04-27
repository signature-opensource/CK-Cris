using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// This interface can appear in any command handler, post handler or event handler.
    /// <para>
    /// It is not available in command validator methods: a validator cannot send events
    /// or execute commands and is given a user message collector to emit its errors and
    /// warnings.
    /// </para>
    /// </summary>
    [EndpointScopedService]
    public interface ICrisEventContext : IScopedAutoService
    {
        /// <summary>
        /// Gets the monitor that can be used to log activities.
        /// </summary>
        IActivityMonitor Monitor { get; }

        /// <summary>
        /// Executes a command in this context.
        /// This must be called sequentially, the returned task must be awaited: no
        /// parallel is supported.
        /// <para>
        /// The result is;
        /// <list type="bullet">
        ///  <item>A <see cref="ICrisResultError"/> on validation or execution error.</item>
        ///  <item>A successful null result on success when this command is a <see cref="ICommand"/>.</item>
        ///  <item>A successful result object (that can be null) if this command is a <see cref="ICommand{TResult}"/>.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <returns>The command result or the <see cref="ICrisResultError"/>.</returns>
        Task<object?> ExecuteCommandAsync( IAbstractCommand command );

        /// <summary>
        /// Configures and executes a command in this context.
        /// This must be called sequentially, the returned task must be awaited: no
        /// parallel is supported.
        /// <para>
        /// The result object is;
        /// <list type="bullet">
        ///  <item>A <see cref="ICrisResultError"/> on validation or execution error.</item>
        ///  <item>A successful null result on success when this command is a <see cref="ICommand"/>.</item>
        ///  <item>A successful result object (that can be null) if this command is a <see cref="ICommand{TResult}"/>.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="configure">A function that configures the command.</param>
        /// <returns>The command result or the <see cref="ICrisResultError"/>.</returns>
        Task<object?> ExecuteCommandAsync<T>( Action<T> configure ) where T : IAbstractCommand;

        /// <summary>
        /// Executes a command in this context, providing a way to inspect its events and,
        /// optionally, to handle them manually.
        /// <para>
        /// This must be called sequentially, the returned task must be awaited: no parallel is supported.
        /// </para>
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="stopEventPropagation">True to not propagate the events to the caller: <see cref="IExecutedCommand.Events"/>
        /// must be manually handled and emitted again if needed.</param>
        /// <returns>The executed command with its events.</returns>
        Task<IExecutedCommand<T>> ExecuteAsync<T>( T command, bool stopEventPropagation = false ) where T : class, IAbstractCommand;

        /// <summary>
        /// Configures and executes a command in this context, providing a way to inspect its events and,
        /// optionally, to handle them manually.
        /// <para>
        /// This must be called sequentially, the returned task must be awaited: no parallel is supported.
        /// </para>
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="stopEventPropagation">True to not propagate the events to the caller: <see cref="IExecutedCommand.Events"/>
        /// must be manually handled and emitted again if needed.</param>
        /// <returns>The executed command with its events.</returns>
        Task<IExecutedCommand<T>> ExecuteAsync<T>( Action<T> configure, bool stopEventPropagation = false ) where T : class, IAbstractCommand;

    }
}
