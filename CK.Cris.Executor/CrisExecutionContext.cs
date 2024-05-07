using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Execution context for Cris command end events.
    /// <para>
    /// This is a scoped service. 
    /// </para>
    /// </summary>
    [Setup.AlsoRegisterType( typeof( RawCrisExecutor ) )]
    [Setup.AlsoRegisterType( typeof( DarkSideCrisEventHub ) )]
    public class CrisExecutionContext : ICrisCommandContext
    {
        readonly IServiceProvider _serviceProvider;
        readonly DarkSideCrisEventHub _eventHub;
        readonly IActivityMonitor _monitor;
        readonly RawCrisExecutor _rawExecutor;

        /// <summary>
        /// Initializes a new execution context. 
        /// </summary>
        /// <param name="monitor">The monitor that must be same as the one resolvable from the provided <paramref name="serviceProvider"/>.</param>
        /// <param name="serviceProvider">The scoped provider. There is unfortunately no safe way to ensure that this provider is a scoped one, so be cautious.</param>
        /// <param name="eventHub">The event hub (sender side).</param>
        /// <param name="rawExecutor">The raw executor singleton.</param>
        public CrisExecutionContext( IActivityMonitor monitor,
                                     IServiceProvider serviceProvider,
                                     DarkSideCrisEventHub eventHub,
                                     RawCrisExecutor rawExecutor )
        {
            _serviceProvider = serviceProvider;
            _eventHub = eventHub;
            _rawExecutor = rawExecutor;
            _monitor = monitor;
            _stack = new List<StackFrame>();
        }

        /// <summary>
        /// Gets whether this context is currently executing a command.
        /// </summary>
        public bool IsExecutingCommand => _stack.Count > 0;

        /// <summary>
        /// Executes a root command: this must not be called when <see cref="IsExecutingCommand"/> is true.
        /// <para>
        /// This never throws:
        /// <list type="bullet">
        ///     <item>
        ///     If the command execution fails, a <see cref="ICrisResultError"/> is the <see cref="IExecutedCommand.Result"/>.
        ///     Note that if immediate events handling fail, the command fails.
        ///     </item>
        ///     <item>
        ///     If the command succeeds and an exception is raised while handling the final routed events, they are logged but don't surface here:
        ///     the command has been successfully executed, the consequences are not its concern.
        ///     </item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="rootCommand">The command to execute.</param>
        /// <returns>The <see cref="IExecutedCommand{T}"/>.</returns>
        public async Task<IExecutedCommand<T>> ExecuteRootCommandAsync<T>( T rootCommand ) where T : class, IAbstractCommand
        {
            Throw.CheckNotNullArgument( rootCommand );
            Throw.CheckState( !IsExecutingCommand );
            StackPush( rootCommand );
            try
            {
                var raw = await _rawExecutor.RawExecuteAsync( _serviceProvider, rootCommand );
                if( raw.Result is ICrisResultError )
                {
                    return new ExecutedCommand<T>( rootCommand, raw.Result, raw.ValidationMessages?.UserMessages, null );
                }
                var finalEvents = StackPeek().Events;
                if( finalEvents != null )
                {
                    // Use index here: new final events can appear because routed event handlers
                    // may execute commands that raise subsequent events.
                    for( int i = 0; i < finalEvents.Count; i++ )
                    {
                        IEvent? e = finalEvents[i];
                        Throw.DebugAssert( "No immediate event here.", e.CrisPocoModel.Kind == CrisPocoKind.RoutedEvent || e.CrisPocoModel.Kind == CrisPocoKind.CallerOnlyEvent );
                        Throw.DebugAssert( "If it is handled then it is a routed event.", !e.CrisPocoModel.IsHandled || e.CrisPocoModel.Kind == CrisPocoKind.RoutedEvent );
                        if( e.CrisPocoModel.Kind == CrisPocoKind.RoutedEvent )
                        {
                            await _eventHub.AllSender.SafeRaiseAsync( _monitor, e );
                            if( e.CrisPocoModel.IsHandled )
                            {
                                await _rawExecutor.SafeDispatchEventAsync( _serviceProvider, e );
                            }
                        }
                    }
                }
                return new ExecutedCommand<T>( rootCommand, raw.Result, raw.ValidationMessages?.UserMessages, finalEvents );
            }
            finally
            {
                _stack.Clear();
            }
        }

        /// <summary>
        /// Must be overridden to call whatever is needed to signal the event to the external world.
        /// This default implementation sends the event to the <see cref="CrisEventHub"/>: this must be called by overriding
        /// implementation.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="routedImmediateEvent">The immediate event emitted by the current execution.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task RaiseImmediateEventAsync( IActivityMonitor monitor, IEvent routedImmediateEvent )
        {
            Throw.DebugAssert( routedImmediateEvent.CrisPocoModel.Kind == CrisPocoKind.RoutedImmediateEvent );
            return _eventHub.ImmediateSender.RaiseAsync( _monitor, routedImmediateEvent );
        }

        /// <summary>
        /// Must be implemented to call whatever is needed to signal the immediate event to the caller.
        /// Does nothing by default.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="callerImmediateEvent">The immediate event emitted by the current execution.</param>
        /// <returns>The awaitable.</returns>
        protected virtual Task RaiseCallerOnlyImmediateEventAsync( IActivityMonitor monitor, IEvent callerImmediateEvent )
        {
            Throw.DebugAssert( callerImmediateEvent.CrisPocoModel.Kind == CrisPocoKind.CallerOnlyImmediateEvent );
            return Task.CompletedTask;
        }

        // Stack is mutable struct based with ref access of the head.
        record struct StackFrame( IAbstractCommand Command, List<IEvent>? Events );
        readonly List<StackFrame> _stack;
        ref StackFrame StackPeek() => ref CollectionsMarshal.AsSpan( _stack )[_stack.Count - 1];
        void StackPush( IAbstractCommand command ) => _stack.Add( new StackFrame( command, null ) );
        List<IEvent>? StackPop()
        {
            var e = StackPeek().Events;
            _stack.RemoveAt( _stack.Count - 1 );
            return e;
        }

        IActivityMonitor ICrisEventContext.Monitor => _monitor;

        Task<object?> ICrisEventContext.ExecuteCommandAsync<T>( Action<T> configure )  => DoExecuteCommandAsync( _eventHub.PocoDirectory.Create( configure ) );

        Task<object?> ICrisEventContext.ExecuteCommandAsync( IAbstractCommand command ) => DoExecuteCommandAsync( command );

        Task<IExecutedCommand<T>> ICrisEventContext.ExecuteAsync<T>( T command, bool stopEventPropagation ) => DoExecuteAsync( command, stopEventPropagation );

        Task<IExecutedCommand<T>> ICrisEventContext.ExecuteAsync<T>( Action<T> configure, bool stopEventPropagation ) => DoExecuteAsync( _eventHub.PocoDirectory.Create( configure ), stopEventPropagation );

        async Task<object?> DoExecuteCommandAsync( IAbstractCommand command )
        {
            Throw.CheckNotNullArgument( command );
            StackPush( command );
            var raw = await _rawExecutor.RawExecuteAsync( _serviceProvider, command );
            var e = StackPop();
            if( e != null ) PropagateEvents( e );
            return raw.Result;
        }

        async Task<IExecutedCommand<T>> DoExecuteAsync<T>( T command, bool stopEventPropagation ) where T : class, IAbstractCommand
        {
            Throw.CheckNotNullArgument( command );
            StackPush( command );
            var raw = await _rawExecutor.RawExecuteAsync( _serviceProvider, command );
            var e = StackPop();
            if( e != null && !stopEventPropagation ) PropagateEvents( e );
            return new ExecutedCommand<T>( command, raw.Result, raw.ValidationMessages?.UserMessages, e );
        }

        void PropagateEvents( List<IEvent> events )
        {
            ref var f = ref StackPeek();
            if( f.Events == null ) f.Events = events;
            else f.Events.AddRange( events );
        }

        Task ICrisCommandContext.EmitEventAsync( IEvent e ) => DoEmitEventAsync( e );

        Task ICrisCommandContext.EmitEventAsync<T>( Action<T> configure ) => DoEmitEventAsync( _eventHub.PocoDirectory.Create( configure ) );

        Task DoEmitEventAsync( IEvent e )
        {
            ref var frame = ref StackPeek();
            if( e is IEventWithCommand c ) c.SourceCommand = frame.Command;
            if( e.CrisPocoModel.Kind == CrisPocoKind.RoutedImmediateEvent )
            {
                if( e.CrisPocoModel.IsHandled )
                {
                    return HandleRoutedImmediateEventAsync( e );
                }
                return RaiseImmediateEventAsync( _monitor, e );
            }
            else if( e.CrisPocoModel.Kind == CrisPocoKind.CallerOnlyImmediateEvent )
            {
                return RaiseCallerOnlyImmediateEventAsync( _monitor, e );
            }
            frame.Events ??= new List<IEvent>();
            frame.Events.Add( e );
            return Task.CompletedTask;
        }

        async Task HandleRoutedImmediateEventAsync( IEvent e )
        {
            await _rawExecutor.DispatchEventAsync( _serviceProvider, e );
            await RaiseImmediateEventAsync( _monitor, e );
        }
    }
}
