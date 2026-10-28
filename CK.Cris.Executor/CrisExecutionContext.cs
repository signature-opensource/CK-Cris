using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CK.Cris;

/// <summary>
/// Execution context for Cris command end events.
/// <para>
/// This is a scoped service but a <see cref="ScopedContainerConfiguredServiceAttribute">[ScopedContainerConfiguredService]</see>
/// one through the base <see cref="ICrisEventContext"/> interface: it is up to each <see cref="DIContainerDefinition{TScopeData}.ConfigureContainerServices(Microsoft.Extensions.DependencyInjection.IServiceCollection, Func{IServiceProvider, TScopeData}, Microsoft.Extensions.DependencyInjection.IServiceProviderIsService)"/>
/// to provide it.
/// </para>
/// </summary>
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

    // A child of the parent bound to other services: it shares the monitor, the hub, the executor and, through the
    // overrides, the immediate events forwarding of the parent. A view also shares the command stack (the events land
    // in the parent's frames); an independent child has its own.
    CrisExecutionContext( CrisExecutionContext parent, IServiceProvider serviceProvider, bool shareStack )
    {
        _serviceProvider = serviceProvider;
        _eventHub = parent._eventHub;
        _rawExecutor = parent._rawExecutor;
        _monitor = parent._monitor;
        _stack = shareStack ? parent._stack : new List<StackFrame>();
    }

    [ExcludedCKType]
    sealed class Child : CrisExecutionContext
    {
        readonly CrisExecutionContext _parent;

        public Child( CrisExecutionContext parent, IServiceProvider serviceProvider, bool shareStack )
            : base( parent, serviceProvider, shareStack )
        {
            _parent = parent;
        }

        protected override Task RaiseImmediateEventAsync( IActivityMonitor monitor, IEvent routedImmediateEvent )
            => _parent.RaiseImmediateEventAsync( monitor, routedImmediateEvent );

        protected override Task RaiseCallerOnlyImmediateEventAsync( IActivityMonitor monitor, IEvent callerImmediateEvent )
            => _parent.RaiseCallerOnlyImmediateEventAsync( monitor, callerImmediateEvent );
    }

    /// <summary>
    /// Creates a view of this context bound to other services (a scope with other ambient services): it shares the
    /// command stack (the events land in the frames of this context), the monitor and the immediate events forwarding.
    /// </summary>
    /// <param name="serviceProvider">The scoped services of the view.</param>
    /// <returns>The view.</returns>
    internal CrisExecutionContext CreateView( IServiceProvider serviceProvider ) => new Child( this, serviceProvider, shareStack: true );

    /// <summary>
    /// Executes a command inline with other ambient services: on the caller's asynchronous flow, in a new scope whose
    /// ambient services are the ones of the <paramref name="hub"/> (see <see cref="ICrisAmbientScopeFactory"/>). Nothing
    /// is queued, so this never waits for a runner.
    /// <para>
    /// This is an independent execution, like the one of a remote party (a tenant switch is a call to another party):
    /// <list type="bullet">
    ///   <item>its routed events are dispatched in the new scope when the command completes (not at the end of the
    ///   caller's root command, and the caller's later failure doesn't undo them);</item>
    ///   <item>its caller-only events are returned to the caller: they land in the current frame of this context;</item>
    ///   <item>its immediate events are forwarded as the ones of this context;</item>
    ///   <item>the commands executed by its handlers run in the new scope.</item>
    /// </list>
    /// The scoped services of the caller are not shared (the new scope has its own). No incoming validation is done
    /// (this is in-process); the handling validators run with the new ambient services.
    /// </para>
    /// </summary>
    /// <param name="hub">The ambient services to use.</param>
    /// <param name="command">The command to execute.</param>
    /// <returns>The raw result.</returns>
    public async Task<RawCrisExecutor.RawResult> RawExecuteInlineAsync( AmbientServiceHub hub, IAbstractCommand command )
    {
        Throw.CheckNotNullArgument( hub );
        Throw.CheckNotNullArgument( command );
        var factory = (ICrisAmbientScopeFactory?)_serviceProvider.GetService( typeof( ICrisAmbientScopeFactory ) );
        if( factory == null )
        {
            Throw.InvalidOperationException( $"No {nameof( ICrisAmbientScopeFactory )} is available: CK.Cris.BackgroundExecutor must be installed to execute a command with other ambient services." );
        }
        CrisExecutionContext? inline = null;
        await using var scope = factory.CreateScope( hub, _monitor, sp => inline = new Child( this, sp, shareStack: false ) );
        Throw.DebugAssert( inline != null );
        var (raw, callerOnlyEvents) = await inline.ExecuteIndependentAsync( command ).ConfigureAwait( false );
        if( callerOnlyEvents != null )
        {
            if( IsExecutingCommand ) PropagateEvents( callerOnlyEvents );
            else _monitor.Warn( $"{callerOnlyEvents.Count} caller-only events of '{command.CrisPocoModel.PocoName}' are lost: the caller is not executing a command." );
        }
        return raw;
    }

    // Executes a command as a root (own stack): its final routed events are dispatched here, its caller-only events
    // are returned.
    async Task<(RawCrisExecutor.RawResult Raw, List<IEvent>? CallerOnlyEvents)> ExecuteIndependentAsync( IAbstractCommand command )
    {
        Throw.DebugAssert( !IsExecutingCommand );
        StackPush( command );
        try
        {
            var raw = await _rawExecutor.RawExecuteAsync( _serviceProvider, command ).ConfigureAwait( false );
            if( raw.Result is ICrisResultError ) return (raw, null);
            var finalEvents = StackPeek().Events;
            if( finalEvents == null ) return (raw, null);
            await DispatchFinalEventsAsync( finalEvents ).ConfigureAwait( false );
            var callerOnly = finalEvents.Where( e => e.CrisPocoModel.Kind == CrisPocoKind.CallerOnlyEvent ).ToList();
            return (raw, callerOnly.Count > 0 ? callerOnly : null);
        }
        finally
        {
            _stack.Clear();
        }
    }

    /// <summary>
    /// Executes a root command inline with other ambient services: for an endpoint whose incoming validation configured
    /// ambient services (see <see cref="RawCrisReceiver"/>). The command executes on the caller's asynchronous flow (nothing
    /// is queued), in a new scope built from the <paramref name="hub"/> (see <see cref="ICrisAmbientScopeFactory"/>), with a
    /// view of this context: its immediate events are the ones of this context, its final routed events are dispatched in the
    /// new scope. This must not be called when <see cref="IsExecutingCommand"/> is true.
    /// </summary>
    /// <param name="hub">The ambient services to use.</param>
    /// <param name="rootCommand">The command to execute.</param>
    /// <param name="validationMessages">Optional pre-existing validation messages (typically incoming validation messages).</param>
    /// <returns>The executed command.</returns>
    public async Task<ExecutedCommand> ExecuteRootCommandInlineAsync( AmbientServiceHub hub,
                                                                      IAbstractCommand rootCommand,
                                                                      ImmutableArray<UserMessage> validationMessages = default )
    {
        Throw.CheckNotNullArgument( hub );
        Throw.CheckNotNullArgument( rootCommand );
        Throw.CheckState( !IsExecutingCommand );
        var factory = (ICrisAmbientScopeFactory?)_serviceProvider.GetService( typeof( ICrisAmbientScopeFactory ) );
        if( factory == null )
        {
            Throw.InvalidOperationException( $"No {nameof( ICrisAmbientScopeFactory )} is available: CK.Cris.BackgroundExecutor must be installed to execute a command with other ambient services." );
        }
        CrisExecutionContext? view = null;
        await using var scope = factory.CreateScope( hub, _monitor, sp => view = CreateView( sp ) );
        Throw.DebugAssert( view != null );
        return await view.ExecuteRootCommandAsync( rootCommand, null, validationMessages ).ConfigureAwait( false );
    }

    /// <summary>
    /// Gets the scoped services of this context.
    /// </summary>
    internal IServiceProvider ServiceProvider => _serviceProvider;

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
        return (IExecutedCommand<T>)await ExecuteRootCommandAsync( rootCommand, default ).ConfigureAwait( false );
    }

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
    /// <param name="deferredExecutionInfo">
    /// Optional <see cref="IDeferredCommandExecutionContext"/> that must be set when the command execution
    /// has been deferred to another context.
    /// </param>
    /// <param name="validationMessages">Optional pre-existing validation messages (typically incoming validation messages).</param>
    /// <returns>The executed command that is a <see cref="IExecutedCommand{T}"/> of the command.</returns>
    public async Task<ExecutedCommand> ExecuteRootCommandAsync( IAbstractCommand rootCommand,
                                                                IDeferredCommandExecutionContext? deferredExecutionInfo = null,
                                                                ImmutableArray<UserMessage> validationMessages = default )
    {
        Throw.CheckNotNullArgument( rootCommand );
        Throw.CheckState( !IsExecutingCommand );
        StackPush( rootCommand );
        try
        {
            var raw = await _rawExecutor.RawExecuteAsync( _serviceProvider, rootCommand );
            if( raw.Result is ICrisResultError )
            {
                return rootCommand.CrisPocoModel.CreateExecutedCommand( rootCommand,
                                                                        raw.Result,
                                                                        CreateValidationMessages( validationMessages, raw ),
                                                                        ImmutableArray<IEvent>.Empty,
                                                                        deferredExecutionInfo );
            }
            var finalEvents = StackPeek().Events;
            if( finalEvents != null )
            {
                await DispatchFinalEventsAsync( finalEvents );
            }
            return rootCommand.CrisPocoModel.CreateExecutedCommand( rootCommand,
                                                                    raw.Result,
                                                                    CreateValidationMessages( validationMessages, raw ),
                                                                    finalEvents != null
                                                                        ? finalEvents.ToImmutableArray()
                                                                        : ImmutableArray<IEvent>.Empty,
                                                                    deferredExecutionInfo );
        }
        finally
        {
            _stack.Clear();
        }

        static ImmutableArray<UserMessage> CreateValidationMessages( ImmutableArray<UserMessage> validationMessages, RawCrisExecutor.RawResult raw )
        {
            return validationMessages.IsDefaultOrEmpty
                    ? (raw.ValidationMessages == null
                        ? ImmutableArray<UserMessage>.Empty
                        : raw.ValidationMessages.UserMessages.ToImmutableArray())
                    : raw.ValidationMessages == null
                        ? validationMessages
                        : validationMessages.Concat( raw.ValidationMessages.UserMessages ).ToImmutableArray();
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
    // Dispatches the final routed events of a root command, in this context's scope.
    async Task DispatchFinalEventsAsync( List<IEvent> finalEvents )
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

    Task<object?> ICrisEventContext.ExecuteCommandAsync<T>( Action<T> configure ) => DoExecuteCommandAsync( _eventHub.PocoDirectory.Create( configure ) );

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
        return new ExecutedCommand<T>( command, raw.Result, deferredExecutionInfo: null, raw.ValidationMessages?.UserMessages, e );
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
        if( e is IEventSourceCommandPart c )
        {
            // A command with sensitive data must never be captured by its events.
            if( frame.Command.CrisPocoModel.HasSensitiveData )
            {
                Throw.InvalidOperationException( $"Command '{frame.Command.CrisPocoModel.PocoName}' has sensitive data: it cannot emit the event '{e.CrisPocoModel.PocoName}' that captures its source command (IEventSourceCommandPart)." );
            }
            c.SourceCommand = frame.Command;
        }
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
