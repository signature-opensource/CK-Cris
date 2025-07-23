using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CK.Setup.Cris;

/// <summary>
/// Command model.
/// </summary>
public sealed partial class CrisType
{
    readonly IPrimaryPocoType _crisPocoType;
    readonly CrisPocoKind _kind;
    readonly int _crisPocoIndex;

    IList<HandlerRoutedEventMethod>? _eventHandlers;
    MultiTargetHandlerList? _ambientServicesConfigurators;
    MultiTargetHandlerList? _ambientServicesRestorers;

    readonly IPocoType? _commandResultType;
    MultiTargetHandlerList? _incomingValidators;
    MultiTargetHandlerList? _handlingValidators;
    IList<HandlerPostMethod>? _postHandlers;
    readonly IStObjFinalClass? _commandHandlerService;
    HandlerMethod? _commandHandler;

    IList<IPrimaryPocoField>? _ambientValues;

    /// <summary>
    /// Gets the command or event type.
    /// </summary>
    public IPrimaryPocoType CrisPocoType => _crisPocoType;

    /// <summary>
    /// Gets the handler method.
    /// Only <see cref="CrisPocoKind.Command"/> and <see cref="CrisPocoKind.CommandWithResult"/> can have a command handler.
    /// </summary>
    public HandlerMethod? CommandHandler => _commandHandler;

    /// <summary>
    /// Gets whether this command or event is handled: a command must have a <see cref="CommandHandler"/> and
    /// an event must have at least one <see cref="EventHandlers"/>. A <see cref="IEvent"/> that is not routed
    /// (has no [RoutedEvent] attribute is never handled.
    /// </summary>
    public bool IsHandled => _commandHandler != null || (_eventHandlers != null && _eventHandlers.Count > 0);

    /// <summary>
    /// Gets the incoming validator methods.
    /// Only <see cref="CrisPocoKind.Command"/> and <see cref="CrisPocoKind.CommandWithResult"/> can have validators.
    /// </summary>
    public MultiTargetHandlerList IncomingValidators => _incomingValidators!;

    /// <summary>
    /// Gets the ambient services configurator methods.
    /// Only <see cref="CrisPocoKind.Command"/> and <see cref="CrisPocoKind.CommandWithResult"/> can have configurators.
    /// </summary>
    public MultiTargetHandlerList AmbientServicesConfigurators => _ambientServicesConfigurators!;

    /// <summary>
    /// Gets the ambient services restorer methods.
    /// </summary>
    public MultiTargetHandlerList AmbientServicesRestorers => _ambientServicesRestorers!;

    /// <summary>
    /// Gets the handling validator methods.
    /// Only <see cref="CrisPocoKind.Command"/> and <see cref="CrisPocoKind.CommandWithResult"/> can have validators.
    /// </summary>
    public MultiTargetHandlerList HandlingValidators => _handlingValidators!;

    /// <summary>
    /// Gets the post handler methods.
    /// Only <see cref="CrisPocoKind.Command"/> and <see cref="CrisPocoKind.CommandWithResult"/> can have post handlers.
    /// </summary>
    public IReadOnlyList<HandlerPostMethod> PostHandlers => (IReadOnlyList<HandlerPostMethod>)_postHandlers!;

    /// <summary>
    /// Gets the event handlers methods.
    /// Only <see cref="CrisPocoKind.RoutedImmediateEvent"/> and <see cref="CrisPocoKind.RoutedEvent"/> can have event handlers.
    /// </summary>
    public IReadOnlyList<HandlerRoutedEventMethod> EventHandlers => (IReadOnlyList<HandlerRoutedEventMethod>)_eventHandlers!;

    /// <summary>
    /// Gets the fields of this Poco that are marked with <see cref="AmbientServiceValueAttribute"/>.
    /// </summary>
    public IReadOnlyList<IPrimaryPocoField> AmbientValueFields => (IReadOnlyList<IPrimaryPocoField>)_ambientValues!;

    /// <summary>
    /// Gets the name of this command or event (this is the <see cref="INamedPocoType.ExternalOrCSharpName"/>).
    /// </summary>
    public string PocoName => _crisPocoType.ExternalOrCSharpName;

    /// <summary>
    /// Gets the <see cref="CrisPocoKind"/>.
    /// </summary>
    public CrisPocoKind Kind => _kind;

    /// <summary>
    /// Gets a unique, zero-based index that identifies this Cris object among all
    /// the <see cref="ICrisDirectoryServiceEngine.CrisTypes"/>.
    /// </summary>
    public int CrisPocoIndex => _crisPocoIndex;

    /// <summary>
    /// Gets the command result type.
    /// </summary>
    public IPocoType? CommandResultType => _commandResultType;

    /// <summary>
    /// Gets whether there are asynchronous post handlers to call.
    /// </summary>
    public bool HasPostHandlerAsyncCall => _postHandlers != null && _postHandlers.Any( h => h.IsRefAsync || h.IsValAsync );

    /// <summary>
    /// Gets the <see cref="IAutoService"/> that must implement the handler method.
    /// </summary>
    public IStObjFinalClass? ExpectedHandlerService => _commandHandlerService;

    /// <summary>
    /// Overridden to return the <see cref="PocoName"/>.
    /// </summary>
    /// <returns>The name of this command.</returns>
    public override string ToString() => PocoName;

    internal CrisType( IPrimaryPocoType crisPocoType,
                       int crisPocoIdx,
                       IPocoType? resultType,
                       IStObjFinalClass? handlerService,
                       CrisPocoKind kind )
    {
        _crisPocoType = crisPocoType;
        _commandHandlerService = handlerService;
        _kind = kind;
        _crisPocoIndex = crisPocoIdx;
        _commandResultType = resultType;
    }

    internal void CloseRegistration( IActivityMonitor monitor )
    {
        _ambientValues ??= Array.Empty<IPrimaryPocoField>();

        if( _kind is CrisPocoKind.Command or CrisPocoKind.CommandWithResult )
        {
            Throw.DebugAssert( _eventHandlers == null );
            _eventHandlers = Array.Empty<HandlerRoutedEventMethod>();
            //
            // Whether the command is handled or not is not the problem of the
            // incoming validators: this enables RawCrisReceiver to be used in
            // a "relay" gateway (that doesn't currently exist but tests exist
            // that are happy to not register a fake handler to test validators!).
            //
            _incomingValidators ??= MultiTargetHandlerList._empty;
            if( _commandHandler == null )
            {
                monitor.Warn( $"Command '{_crisPocoType.ExternalOrCSharpName}' is not handled. Forgetting {_handlingValidators?.Count ?? 0} validator, {_ambientServicesConfigurators?.Count ?? 0} ambient service configurators, {_ambientServicesRestorers?.Count ?? 0} ambient service restorers and {_postHandlers?.Count ?? 0} post handlers." );
                _handlingValidators = MultiTargetHandlerList._empty;
                _ambientServicesConfigurators = MultiTargetHandlerList._empty;
                _ambientServicesRestorers = MultiTargetHandlerList._empty;
                _postHandlers = Array.Empty<HandlerPostMethod>();
            }
            else
            {
                _handlingValidators ??= MultiTargetHandlerList._empty;
                _ambientServicesConfigurators ??= MultiTargetHandlerList._empty;
                _ambientServicesRestorers ??= MultiTargetHandlerList._empty;
                _postHandlers ??= Array.Empty<HandlerPostMethod>();
            }
        }
        else if( _kind is CrisPocoKind.RoutedImmediateEvent or CrisPocoKind.RoutedEvent )
        {
            Throw.DebugAssert( _incomingValidators == null );
            _incomingValidators = MultiTargetHandlerList._empty;
            Throw.DebugAssert( _handlingValidators == null );
            _handlingValidators = MultiTargetHandlerList._empty;
            Throw.DebugAssert( _postHandlers == null );
            _postHandlers = Array.Empty<HandlerPostMethod>();
            if( _eventHandlers == null )
            {
                monitor.Warn( $"Routed event '{_crisPocoType.ExternalOrCSharpName}' is not handled. Forgetting {_ambientServicesConfigurators?.Count ?? 0} ambient service configurators and {_ambientServicesRestorers?.Count ?? 0} ambient service restorers." );
                _ambientServicesConfigurators = MultiTargetHandlerList._empty;
                _ambientServicesRestorers = MultiTargetHandlerList._empty;
                _eventHandlers = Array.Empty<HandlerRoutedEventMethod>();
            }
            else
            {
                _ambientServicesConfigurators ??= MultiTargetHandlerList._empty;
                _ambientServicesRestorers ??= MultiTargetHandlerList._empty;
            }
        }
        else
        {
            Throw.DebugAssert( _kind is CrisPocoKind.CallerOnlyImmediateEvent or CrisPocoKind.CallerOnlyEvent );
            Throw.DebugAssert( _incomingValidators == null );
            _incomingValidators = MultiTargetHandlerList._empty;
            Throw.DebugAssert( _ambientServicesConfigurators == null );
            _ambientServicesConfigurators = MultiTargetHandlerList._empty;
            Throw.DebugAssert( _ambientServicesRestorers == null );
            _ambientServicesRestorers = MultiTargetHandlerList._empty;
            Throw.DebugAssert( _handlingValidators == null );
            _handlingValidators = MultiTargetHandlerList._empty;
            Throw.DebugAssert( _postHandlers == null );
            _postHandlers = Array.Empty<HandlerPostMethod>();
            Throw.DebugAssert( _eventHandlers == null );
            _eventHandlers = Array.Empty<HandlerRoutedEventMethod>();
        }
    }

    internal void AddAmbientValueField( IPrimaryPocoField f )
    {
        _ambientValues ??= new List<IPrimaryPocoField>();
        _ambientValues.Add( f );
    }

    internal static string MethodName( MethodInfo m, ParameterInfo[]? parameters = null ) => $"{m.DeclaringType!.Name}.{m.Name}( {(parameters ?? m.GetParameters()).Select( p => p.ParameterType.Name + " " + p.Name ).Concatenate()} )";

    enum BestCommandHandler
    {
        Ambiguous,
        KeepCurrent,
        KeepNew
    }

    internal bool AddCommandHandler( IActivityMonitor monitor,
                                     IPocoTypeSystem typeSystem,
                                     IStObjFinalClass owner,
                                     MethodInfo method,
                                     ParameterInfo[] parameters,
                                     ParameterInfo command,
                                     bool isClosedHandler,
                                     string? fileName,
                                     int lineNumber )
    {
        var (unwrappedReturnType, isRefAsync, isValAsync) = GetReturnParameterInfo( method );

        // First, check the returned type. We always skip an handler that returns a type
        // not assignable to the ICommand<TResult>.
        IPocoType? handlerUnwrappedReturnType = null;
        if( _commandResultType == null )
        {
            if( unwrappedReturnType != typeof( void ) )
            {
                monitor.Warn( $"Handler method '{MethodName( method, parameters )}' must not return any value but returns a '{unwrappedReturnType.Name}'. This handler is skipped." );
                return true;
            }
        }
        else
        {
            handlerUnwrappedReturnType = typeSystem.FindByType( unwrappedReturnType );
            if( handlerUnwrappedReturnType == null ||
                (handlerUnwrappedReturnType != _commandResultType && !handlerUnwrappedReturnType.IsSubTypeOf( _commandResultType )) )
            {
                monitor.Warn( $"Handler method '{MethodName( method, parameters )}': expected return type is '{_commandResultType.Type:N}' but '{unwrappedReturnType:N}' is not compatible. This handler is skipped." );
                return true;
            }
        }
        if( _commandHandlerService != null && owner != _commandHandlerService )
        {
            monitor.Info( $"""
                        Handler method '{MethodName( method, parameters )}' is skipped.
                        The command handler is implemented by '{_commandHandlerService.ClassType:N}' that is a 'ICommandHandler<{PocoName}>' service).
                        """ );
            return true;
        }
        if( _commandHandler != null )
        {
            switch( ElectBestCommandHandlerParameterWise( monitor, method, parameters, command, isClosedHandler ) )
            {
                case BestCommandHandler.KeepCurrent: return true;
                case BestCommandHandler.KeepNew:
                    break;
                case BestCommandHandler.Ambiguous:
                    if( _commandHandler.UnwrappedReturnType == handlerUnwrappedReturnType )
                    {
                        monitor.Error( $"Ambiguity: both '{MethodName( method, parameters )}' and '{_commandHandler}' handle '{PocoName}' command and returns the same result." );
                        return false;
                    }
                    Throw.DebugAssert( "All void return cases have been handled.",
                                       _commandHandler.UnwrappedReturnType != null && handlerUnwrappedReturnType != null );
                    if( _commandHandler.UnwrappedReturnType.IsSubTypeOf( handlerUnwrappedReturnType ) )
                    {
                        InfoSpecializedReturnSkipped( monitor, MethodName( method, parameters ), _commandHandler.ToString(), PocoName );
                        return true;
                    }
                    if( handlerUnwrappedReturnType.IsSubTypeOf( _commandHandler.UnwrappedReturnType ) )
                    {
                        InfoSpecializedReturnSkipped( monitor, _commandHandler.ToString(), MethodName( method, parameters ), PocoName );
                    }
                    else
                    {
                        monitor.Error( $"""
                            Ambiguity: cannot choose between the following handlers for '{PocoName}' command as they return unrelated types:
                            {MethodName( method, parameters )} returns '{handlerUnwrappedReturnType.CSharpName}'
                            and
                            {MethodName( _commandHandler.Method, _commandHandler.Parameters )} returns '{_commandHandler.UnwrappedReturnType.CSharpName}'
                            """ );
                        return false;
                    }
                    break;
            }
        }
        _commandHandler = new HandlerMethod( this, owner, method, parameters, fileName, lineNumber, command, handlerUnwrappedReturnType, isRefAsync, isValAsync, isClosedHandler );
        CheckSyncAsyncMethodName( monitor, method, parameters, _commandHandler.IsRefAsync || _commandHandler.IsValAsync );
        return true;

        static void InfoSpecializedReturnSkipped( IActivityMonitor monitor, string skipped, string kept, string pocoName )
        {
            monitor.Info( $"Handler method '{skipped}' is skipped since '{kept}' returns a more specialized result for '{pocoName}' command." );
        }
    }

    BestCommandHandler ElectBestCommandHandlerParameterWise( IActivityMonitor monitor,
                                                             MethodInfo method,
                                                             ParameterInfo[] parameters,
                                                             ParameterInfo command,
                                                             bool isClosedHandler )
    {
        Throw.DebugAssert( _commandHandler != null );
        if( _commandHandler.IsClosedHandler )
        {
            if( isClosedHandler )
            {
                return BestCommandHandler.Ambiguous;
            }
            InfoUnclosedSkipped( monitor, method, parameters );
            return BestCommandHandler.KeepCurrent;
        }
        Throw.DebugAssert( "Current handler is an unclosed one.",
                            !_commandHandler.IsClosedHandler );
        if( isClosedHandler )
        {
            // Skip the current one.
            InfoUnclosedSkipped( monitor, _commandHandler.Method, _commandHandler.Parameters );
            return BestCommandHandler.KeepNew;
        }
        // Two unclosed handlers. Use the two command Parameter types to decide.
        // - cur == new => Ambiguity.
        // - cur is assignable from new => new
        // - new is assignable from cur => cur
        // - new independent of cur => Ambiguity.
        var curCT = _commandHandler.CommandParameter.ParameterType;
        var newCT = command.ParameterType;
        if( curCT == newCT )
        {
            return BestCommandHandler.Ambiguous;
        }
        if( newCT.IsAssignableFrom( curCT ) )
        {
            InfoSpecializedSkipped( monitor, MethodName( method, parameters ), _commandHandler.ToString(), PocoName );
            return BestCommandHandler.KeepCurrent;
        }
        if( curCT.IsAssignableFrom( newCT ) )
        {
            // Not ideal: MethodName will be recomputed
            InfoSpecializedSkipped( monitor, _commandHandler.ToString(), MethodName( method, parameters ), PocoName );
            return BestCommandHandler.KeepNew;
        }
        return BestCommandHandler.Ambiguous;

        static void InfoUnclosedSkipped( IActivityMonitor monitor, MethodInfo method, ParameterInfo[] parameters )
        {
            monitor.Info( $"Handler method '{MethodName( method, parameters )}' for unclosed command type is skipped since a closed handler is available." );
        }

        static void InfoSpecializedSkipped( IActivityMonitor monitor, string skipped, string kept, string pocoName )
        {
            monitor.Info( $"Handler method '{skipped}' is skipped since '{kept}' handles a specialized '{pocoName}' command." );
        }
    }

    internal bool AddMultiTargetHandler( IActivityMonitor monitor,
                                         MultiTargetHandlerKind target,
                                         IStObjFinalClass owner,
                                         MethodInfo method,
                                         ParameterInfo[] parameters,
                                         ParameterInfo cmdOrPartParameter,
                                         ParameterInfo? argumentParameter,
                                         ParameterInfo? argumentParameter2,
                                         string? fileName,
                                         int lineNumber )
    {
        if( !CheckVoidReturn( monitor,
                              Enum.GetName( typeof( MultiTargetHandlerKind ), target )!,
                              method,
                              parameters,
                              out bool isRefAsync,
                              out bool isValAsync ) )
        {
            return false;
        }

        void Add( CrisHandlerKind kind, ref MultiTargetHandlerList? list )
        {
            list ??= new MultiTargetHandlerList();
            list.Add( new HandlerMultiTargetMethod( this,
                                                    kind,
                                                    owner,
                                                    method,
                                                    parameters,
                                                    fileName,
                                                    lineNumber,
                                                    cmdOrPartParameter,
                                                    argumentParameter,
                                                    argumentParameter2,
                                                    isRefAsync,
                                                    isValAsync ) );
        }

        switch( target )
        {
            case MultiTargetHandlerKind.IncomingValidator: Add( CrisHandlerKind.IncomingValidator, ref _incomingValidators ); break;
            case MultiTargetHandlerKind.CommandHandlingValidator: Add( CrisHandlerKind.CommandHandlingValidator, ref _handlingValidators ); break;
            case MultiTargetHandlerKind.ConfigureAmbientServices: Add( CrisHandlerKind.ConfigureAmbientServices, ref _ambientServicesConfigurators ); break;
            case MultiTargetHandlerKind.RestoreAmbientServices: Add( CrisHandlerKind.RestoreAmbientServices, ref _ambientServicesRestorers ); break;
            case MultiTargetHandlerKind.RoutedEventHandler:
            {
                if( _kind != CrisPocoKind.RoutedImmediateEvent && _kind != CrisPocoKind.RoutedEvent )
                {
                    monitor.Warn( $"Method '{MethodName( method, parameters )}' will never be called: event '{PocoName}' is not decorated with [RoutedEvent] attribute (it can only be observed by the caller)." );
                }
                else
                {
                    _eventHandlers ??= new List<HandlerRoutedEventMethod>();
                    _eventHandlers.Add( new HandlerRoutedEventMethod( this, owner, method, parameters, fileName, lineNumber, cmdOrPartParameter, isRefAsync, isValAsync ) );
                }
                break;
            }
        }
        return true;
    }

    internal bool AddPostHandler( IActivityMonitor monitor,
                                  IPocoTypeSystem typeSystem,
                                  IStObjFinalClass owner,
                                  MethodInfo method,
                                  ParameterInfo[] parameters,
                                  ParameterInfo commandParameter,
                                  string? fileName,
                                  int lineNumber )
    {
        if( !CheckVoidReturn( monitor, "CommandPostHandler", method, parameters, out bool isRefAsync, out bool isValAsync ) ) return false;

        // Looking for the command result in the parameters.
        bool mustCastResultParameter = false;
        ParameterInfo? resultParameter = null;
        if( _commandResultType != null )
        {
            // Elects the first parameter that is compatible with the result type.
            resultParameter = parameters.FirstOrDefault( p => typeSystem.FindByType( p.ParameterType )?.IsSubTypeOf( _commandResultType ) ?? false );
            if( resultParameter != null ) mustCastResultParameter = resultParameter.ParameterType != _commandResultType.Type;
        }
        if( resultParameter != null )
        {
            monitor.Trace( $"PostHandler method '{MethodName( method, parameters )}': parameter '{resultParameter.Name}' is the Command's result." );
        }
        _postHandlers ??= new List<HandlerPostMethod>();
        _postHandlers.Add( new HandlerPostMethod( this,
                                                  owner,
                                                  method,
                                                  parameters,
                                                  fileName,
                                                  lineNumber,
                                                  commandParameter,
                                                  resultParameter,
                                                  mustCastResultParameter,
                                                  isRefAsync,
                                                  isValAsync ) );
        return true;
    }

    static (Type Unwrapped, bool IsRefAsync, bool IsValAsync) GetReturnParameterInfo( MethodInfo m )
    {
        bool isRefAsync = false, isValAsync = false;
        Type t = m.ReturnParameter.ParameterType;
        if( t == typeof( Task ) )
        {
            t = typeof( void );
            isRefAsync = true;
        }
        else if( t == typeof( ValueTask ) )
        {
            t = typeof( void );
            isValAsync = true;
        }
        else if( t.IsGenericType )
        {
            if( t.GetGenericTypeDefinition() == typeof( Task<> ) )
            {
                t = t.GetGenericArguments()[0];
                isRefAsync = true;
            }
            else if( t.GetGenericTypeDefinition() == typeof( ValueTask<> ) )
            {
                t = t.GetGenericArguments()[0];
                isValAsync = true;
            }
        }
        return (t, isRefAsync, isValAsync);
    }

    static bool CheckVoidReturn( IActivityMonitor monitor, string attrName, MethodInfo method, ParameterInfo[] parameters, out bool isRefAsync, out bool isValAsync )
    {
        Type unwrappedReturnType;
        (unwrappedReturnType, isRefAsync, isValAsync) = GetReturnParameterInfo( method );
        if( unwrappedReturnType != typeof( void ) )
        {
            monitor.Error( $"[{attrName}] method '{MethodName( method, parameters )}' must not return any value. Its current returned type is '{unwrappedReturnType.Name}'." );
            return false;
        }
        CheckSyncAsyncMethodName( monitor, method, parameters, isRefAsync || isValAsync );
        return true;
    }


    static void CheckSyncAsyncMethodName( IActivityMonitor monitor, MethodInfo method, ParameterInfo[] parameters, bool isAsyncRet )
    {
        bool isAsyncName = method.Name.EndsWith( "Async", StringComparison.OrdinalIgnoreCase );
        if( isAsyncName && !isAsyncRet )
        {
            monitor.Warn( $"Method name ends with Async but returned type is not a Task or a ValueTask. Method: {MethodName( method, parameters )}." );
        }
        else if( !isAsyncName && isAsyncRet )
        {
            monitor.Warn( $"Method name doesn't end with Async but returned type is a Task or a ValueTask. Method: {MethodName( method, parameters )}." );
        }
    }

}
