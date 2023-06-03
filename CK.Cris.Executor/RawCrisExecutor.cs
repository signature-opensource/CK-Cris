using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Executes commands on services provided to <see cref="RawExecuteAsync(IServiceProvider, IAbstractCommand)"/> and
    /// dispatches events thanks to <see cref="DispatchEventAsync(IServiceProvider, IEvent)"/>.
    /// <para>
    /// This class is agnostic of the context since the <see cref="IServiceProvider"/> defines the execution context: this is a true
    /// singleton, the same instance can be used to execute any locally handled commands.
    /// </para>
    /// <para>
    /// The concrete class implements all the generated code that routes the command to its handler.
    /// </para>
    /// </summary>
    [CK.Setup.ContextBoundDelegation( "CK.Setup.Cris.RawCrisExecutorImpl, CK.Cris.Executor.Engine" )]
    public abstract class RawCrisExecutor : ISingletonAutoService
    {
        /// <summary>
        /// Executes a command by calling the discovered handler and post handlers.
        /// Any exceptions are thrown (or more precisely are set on the returned <see cref="Task"/>).
        /// <para>
        /// A <see cref="IActivityMonitor"/> and a <see cref="ICrisExecutionContext"/> (that is
        /// a <see cref="ICrisCallContext"/>) must be resolvable from the <paramref name="services"/>.
        /// </para>
        /// </summary>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>The result of the <see cref="ICommand{TResult}"/> if the command has a result.</returns>
        public abstract Task<object?> RawExecuteAsync( IServiceProvider services, IAbstractCommand command );

        /// <summary>
        /// Dispatches an event by calling the discovered routed event handlers.
        /// Any exceptions are thrown (or more precisely are set on the returned <see cref="Task"/>).
        /// <para>
        /// A <see cref="IActivityMonitor"/> and a <see cref="ICrisExecutionContext"/> (that is
        /// a <see cref="ICrisCallContext"/>) must be resolvable from the <paramref name="services"/>.
        /// (The execution context cannot be used directly by an event handler, it may be used by the commands that
        /// an event handler can execute).
        /// </para>
        /// </summary>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="e">The event to dispatch to its routed event handlers.</param>
        /// <returns>The awaitable.</returns>
        public abstract Task DispatchEventAsync( IServiceProvider services, IEvent e );

        /// <summary>
        /// Dispatches an event by calling the discovered routed event handlers.
        /// Exceptions are caught and logged and false is returned.
        /// <para>
        /// A <see cref="IActivityMonitor"/> and a <see cref="ICrisExecutionContext"/> (that is
        /// a <see cref="ICrisCallContext"/>) must be resolvable from the <paramref name="services"/>.
        /// (The execution context cannot be used directly by an event handler, it may be used by the commands that
        /// an event handler can execute).
        /// </para>
        /// </summary>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="e">The event to dispatch to its routed event handlers.</param>
        /// <returns>True on success, false if an exception has been caught and logged.</returns>
        public abstract Task<bool> SafeDispatchEventAsync( IServiceProvider services, IEvent e );
    }
}
