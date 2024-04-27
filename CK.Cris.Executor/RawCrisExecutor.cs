using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static CK.Core.CheckedWriteStream;

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
    [Setup.AlsoRegisterType( typeof( CrisDirectory ) )]
    [CK.Setup.ContextBoundDelegation( "CK.Setup.Cris.RawCrisExecutorImpl, CK.Cris.Executor.Engine" )]
    public abstract class RawCrisExecutor : ISingletonAutoService
    {
        /// <summary>
        /// Captures the result of <see cref="RawExecuteAsync(IServiceProvider, IAbstractCommand)"/>.
        /// </summary>
        /// <param name="Result">The execution result. See <see cref="IExecutedCommand.Result"/>.</param>
        /// <param name="ValidationMessages">
        /// Optional user validation messages.
        /// This is never null if the result is a <see cref="ICrisResultError"/> validation error (and at least one
        /// of the message is an error message) or if [CommandValidator] methods have emitted <see cref="UserMessageLevel.Info"/>
        /// or <see cref="UserMessageLevel.Warn"/> messages.
        /// </param>
        public readonly record struct RawResult( object? Result, UserMessageCollector? ValidationMessages );

        /// <summary>
        /// Executes a command by calling the discovered validators (not the <see cref="CommandEndpointValidatorAttribute"/>),
        /// the handler and the post handlers.
        /// <para>
        /// This never throws: a <see cref="ICrisResultError"/> is the <see cref="RawResult.Result"/> on error.
        /// </para>
        /// </summary>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>The raw result.</returns>
        public abstract Task<RawResult> RawExecuteAsync( IServiceProvider services, IAbstractCommand command );

        /// <summary>
        /// Dispatches an event by calling the discovered routed event handlers.
        /// Any exceptions are thrown (or more precisely are set on the returned <see cref="Task"/>).
        /// <para>
        /// A <see cref="IActivityMonitor"/> and a <see cref="ICrisCommandContext"/> (that is
        /// a <see cref="ICrisEventContext"/>) must be resolvable from the <paramref name="services"/>.
        /// (The execution context cannot be used directly by an event handler, it may be used by commands that
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
        /// A <see cref="IActivityMonitor"/> and a <see cref="ICrisCommandContext"/> (that is
        /// a <see cref="ICrisEventContext"/>) must be resolvable from the <paramref name="services"/>.
        /// (The execution context cannot be used directly by an event handler, it may be used by commands that
        /// an event handler can execute).
        /// </para>
        /// </summary>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="e">The event to dispatch to its routed event handlers.</param>
        /// <returns>True on success, false if an exception has been caught and logged.</returns>
        public abstract Task<bool> SafeDispatchEventAsync( IServiceProvider services, IEvent e );

        protected static string? LogValidationError( IServiceProvider services, ICrisPoco command, UserMessageCollector c )
        {
            IActivityMonitor? monitor = (IActivityMonitor?)services.GetService( typeof( IActivityMonitor ) );
            if( monitor != null )
            {
                return RawCrisEndpointValidator.LogValidationError( monitor, command, c, "handling", null );
            }
            ActivityMonitor.StaticLogger.Error( $"Command '{command.CrisPocoModel.PocoName}' handling validation error. (No IActivityMonitor available.)" );
            return null;
        }

    }
}
