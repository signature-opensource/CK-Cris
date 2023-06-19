using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// This is somehow on the dark side: an execution context lives on the command execution side. For
    /// any background or deferred execution, this belongs to the endpoint container DI.
    /// <para>
    /// This is a scoped service that is handled by the Automatic DI but when used in endpoint (for background
    /// or deferred executors), a dedicated specialization is used. 
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

        bool IsExecutingCommand => _stack.Count > 0;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="rootCommand">The command to execute.</param>
        /// <returns>The command result (if any) and the final non immediate events that have been emitted by the command execution.</returns>
        public async Task<(object? Result, IReadOnlyList<IEvent> FinalEvents)> ExecuteAsync( IAbstractCommand rootCommand )
        {
            Throw.CheckNotNullArgument( rootCommand );
            Throw.CheckState( !IsExecutingCommand );
            StackPush( rootCommand );
            try
            {
                var result = await _rawExecutor.RawExecuteAsync( _serviceProvider, rootCommand );
                var finalEvents = (IReadOnlyList<IEvent>?)StackPeek().Events ?? Array.Empty<IEvent>();
                for( int i = 0; i < finalEvents.Count; ++i )
                {
                    var e = finalEvents[i];
                    Debug.Assert( e.CrisPocoModel.Kind == CrisPocoKind.RoutedEvent || e.CrisPocoModel.Kind == CrisPocoKind.CallerOnlyEvent, "No immediate event here." );
                    Debug.Assert( !e.CrisPocoModel.IsHandled || e.CrisPocoModel.Kind == CrisPocoKind.RoutedEvent, "If it is handled then it is a route event." );
                    if( e.CrisPocoModel.Kind == CrisPocoKind.RoutedEvent )
                    {
                        await _eventHub.AllSender.SafeRaiseAsync( _monitor, e );
                        if( e.CrisPocoModel.IsHandled )
                        {
                            await _rawExecutor.DispatchEventAsync( _serviceProvider, e );
                        }
                    }
                }
                return (result, finalEvents);
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
            Debug.Assert( routedImmediateEvent.CrisPocoModel.Kind == CrisPocoKind.RoutedImmediateEvent );
            return _eventHub.ImmediateSender.SafeRaiseAsync( _monitor, routedImmediateEvent );
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
            Debug.Assert( callerImmediateEvent.CrisPocoModel.Kind == CrisPocoKind.CallerOnlyImmediateEvent );
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

        Task<object?> ICrisEventContext.ExecuteCommandAsync<T>( Action<T> configure )  => DoExecuteCommandAsync( _eventHub.PocoDirectory.Create<T>( configure ) );

        Task<object?> ICrisEventContext.ExecuteCommandAsync( IAbstractCommand command ) => DoExecuteCommandAsync( command );

        Task<IExecutedCommand<T>> ICrisEventContext.ExecuteAsync<T>( T command, bool stopEventPropagation ) => DoExecuteAsync( command, stopEventPropagation );

        Task<IExecutedCommand<T>> ICrisEventContext.ExecuteAsync<T>( Action<T> configure, bool stopEventPropagation ) => DoExecuteAsync( _eventHub.PocoDirectory.Create<T>( configure ), stopEventPropagation );

        async Task<object?> DoExecuteCommandAsync( IAbstractCommand command )
        {
            Throw.CheckNotNullArgument( command );
            StackPush( command );
            var r = await _rawExecutor.RawExecuteAsync( _serviceProvider, command );
            var e = StackPop();
            if( e != null ) PropagateEvents( e );
            return r;
        }

        async Task<IExecutedCommand<T>> DoExecuteAsync<T>( T command, bool stopEventPropagation ) where T : class, IAbstractCommand
        {
            Throw.CheckNotNullArgument( command );
            StackPush( command );
            var r = await _rawExecutor.RawExecuteAsync( _serviceProvider, command );
            var e = StackPop();
            if( e != null && !stopEventPropagation ) PropagateEvents( e );
            return new ExecutedCommand<T>( command, r, e );
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
