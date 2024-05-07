using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Configures a <see cref="AmbientServiceHub"/>, executes commands and dispatches events.
    /// <para>
    /// This class is agnostic of the context since the <see cref="IServiceProvider"/> defines the execution context: this is a true
    /// singleton, the same instance can be used to execute any locally handled commands.
    /// </para>
    /// <para>
    /// This is the low level API, the concrete class implements all the generated code that does the hard work.
    /// <see cref="ICrisExecution"/>
    /// </para>
    /// </summary>
    [Setup.AlsoRegisterType( typeof( CrisDirectory ) )]
    [CK.Setup.ContextBoundDelegation( "CK.Setup.Cris.RawCrisExecutorImpl, CK.Cris.Executor.Engine" )]
    public abstract class RawCrisExecutor : ISingletonAutoService
    {
        /// <summary>
        /// Configures the ambient services hub by calling the [ConfigureAmbientService] methods for the command, event or its parts.
        /// This does nothing if <see cref="HasAmbientServicesConfigurators"/> is false.
        /// <para>
        /// This must be called before executing the command or handling the event in a background context, <see cref="AmbientServiceHub.IsLocked"/>
        /// must be false. When called with a fresh AmbientServiceHub obtained from the current context, if <see cref="AmbientServiceHub.IsDirty"/> is false,
        /// the action may be executed inline, directly in the current context but if IsDirty is true then a background execution context should be used.
        /// </para>
        /// <para>
        /// This never throws: any exception is caught and non null a <see cref="ICrisResultError"/> is returned on error.
        /// </para>
        /// </summary>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="crisPoco">The command or event that must be executed or dispatched.</param>
        /// <param name="ambientServices">The ambient services hub to configure.</param>
        public abstract ValueTask<ICrisResultError?> ConfigureAmbientServicesAsync( IServiceProvider services, ICrisPoco crisPoco, AmbientServiceHub ambientServices );

        /// <summary>
        /// Captures the result of <see cref="RawExecuteAsync(IServiceProvider, IAbstractCommand)"/>.
        /// </summary>
        /// <param name="Result">The execution result. See <see cref="IExecutedCommand.Result"/>.</param>
        /// <param name="ValidationMessages">
        /// Optional user validation messages.
        /// This is never null if the result is a <see cref="ICrisResultError"/> validation error (and at least one
        /// of the message is an error message) or if [CommandHandlingValidator] methods have emitted <see cref="UserMessageLevel.Info"/>
        /// or <see cref="UserMessageLevel.Warn"/> messages.
        /// </param>
        public readonly record struct RawResult( object? Result, UserMessageCollector? ValidationMessages );

        /// <summary>
        /// Executes a command by calling the discovered handling validators (not the <see cref="CommandIncomingValidatorAttribute"/>),
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
        public async Task<bool> SafeDispatchEventAsync( IServiceProvider services, IEvent e )
        {
            try
            {
                await DispatchEventAsync( services, e ).ConfigureAwait( false );
                return true;
            }
            catch( Exception ex )
            {
                var monitor = (IActivityMonitor?)services.GetService( typeof( IActivityMonitor ) );
                var msg = $"Event '{e.CrisPocoModel.PocoName}' dispatch failed.";
                if( monitor != null )
                {
                    using( monitor.OpenError( msg, ex ) )
                    {
                        monitor.Trace( e.ToString() ?? string.Empty );
                    }
                }
                else
                {
                    ActivityMonitor.StaticLogger.Error( msg + " (No IActivityMonitor available.)", ex );
                }
                return false;
            }
        }

        /// <summary>
        /// Infrastructure code not intended to be used directly.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="command">The command.</param>
        /// <param name="c">The collector with errors.</param>
        /// <returns>The LogKey (may be null).</returns>
        protected static string? LogValidationError( IServiceProvider services, ICrisPoco command, UserMessageCollector c )
        {
            IActivityMonitor? monitor = (IActivityMonitor?)services.GetService( typeof( IActivityMonitor ) );
            if( monitor != null )
            {
                return RawCrisReceiver.LogValidationError( monitor, command, c, "handling", null );
            }
            ActivityMonitor.StaticLogger.Error( $"Command '{command.CrisPocoModel.PocoName}' handling validation error. (No IActivityMonitor available.)" );
            return null;
        }

    }
}
