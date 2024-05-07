using CK.CodeGen;
using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Command model.
    /// </summary>
    public sealed partial class CrisType
    {
        readonly IPrimaryPocoType _crisPocoType;
        readonly CrisPocoKind _kind;
        readonly int _crisPocoIndex;

        IList<HandlerRoutedEventMethod>? _eventHandlers;

        readonly IPocoType? _commandResultType;
        IList<HandlerMultiTargetMethod>? _incomingValidators;
        IList<HandlerMultiTargetMethod>? _ambientServicesConfigurators;
        IList<HandlerMultiTargetMethod>? _handlingValidators;
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
        public IReadOnlyList<HandlerMultiTargetMethod> IncomingValidators => (IReadOnlyList<HandlerMultiTargetMethod>)_incomingValidators!;

        /// <summary>
        /// Gets the ambient services configurator methods.
        /// Only <see cref="CrisPocoKind.Command"/> and <see cref="CrisPocoKind.CommandWithResult"/> can have validators.
        /// </summary>
        public IReadOnlyList<HandlerMultiTargetMethod> AmbientServicesConfigurators => (IReadOnlyList<HandlerMultiTargetMethod>)_ambientServicesConfigurators!;

        /// <summary>
        /// Gets the handling validator methods.
        /// Only <see cref="CrisPocoKind.Command"/> and <see cref="CrisPocoKind.CommandWithResult"/> can have validators.
        /// </summary>
        public IReadOnlyList<HandlerMultiTargetMethod> HandlingValidators => (IReadOnlyList<HandlerMultiTargetMethod>)_handlingValidators!;

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
        /// the <see cref="CrisPocoModels"/>.
        /// </summary>
        public int CrisPocoIndex => _crisPocoIndex;

        /// <summary>
        /// Gets the command result type.
        /// </summary>
        public IPocoType? CommandResultType => _commandResultType;

        /// <summary>
        /// Gets whether there are asynchronous post handlers to call.
        /// </summary>
        /// <param name="e">The entry.</param>
        /// <returns>True if asynchronous calls must be made.</returns>
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
                _incomingValidators ??= Array.Empty<HandlerMultiTargetMethod>();
                if( _commandHandler == null )
                {
                    monitor.Warn( $"Command '{
                                    _crisPocoType.ExternalOrCSharpName}' is not handled. Forgetting {
                                    _handlingValidators?.Count ?? 0} validator, {
                                    _handlingValidators?.Count ?? 0} ambient service configurators and {
                                    _postHandlers?.Count ?? 0} post handlers." );
                    _handlingValidators = Array.Empty<HandlerMultiTargetMethod>();
                    _ambientServicesConfigurators = Array.Empty<HandlerMultiTargetMethod>();
                    _postHandlers = Array.Empty<HandlerPostMethod>();
                }
                else
                {
                    _ambientServicesConfigurators ??= Array.Empty<HandlerMultiTargetMethod>();
                    _handlingValidators ??= Array.Empty<HandlerMultiTargetMethod>();
                    _postHandlers ??= Array.Empty<HandlerPostMethod>();
                }
            }
            else if( _kind is CrisPocoKind.RoutedImmediateEvent or CrisPocoKind.RoutedEvent )
            {
                Throw.DebugAssert( _incomingValidators == null );
                _incomingValidators = Array.Empty<HandlerMultiTargetMethod>();
                Throw.DebugAssert( _handlingValidators == null );
                _handlingValidators = Array.Empty<HandlerMultiTargetMethod>();
                Throw.DebugAssert( _postHandlers == null );
                _postHandlers = Array.Empty<HandlerPostMethod>();
                if( _eventHandlers == null )
                {
                    monitor.Warn( $"Routed event '{_crisPocoType.ExternalOrCSharpName}' is not handled. Forgetting {
                                   _ambientServicesConfigurators?.Count?? 0} ambient service configurators." );
                    _ambientServicesConfigurators = Array.Empty<HandlerMultiTargetMethod>();
                    _eventHandlers = Array.Empty<HandlerRoutedEventMethod>();
                }
                else
                {
                    _ambientServicesConfigurators ??= Array.Empty<HandlerMultiTargetMethod>();
                }
            }
            else
            {
                Throw.DebugAssert( _kind is CrisPocoKind.CallerOnlyImmediateEvent or CrisPocoKind.CallerOnlyEvent );
                Throw.DebugAssert( _incomingValidators == null );
                _incomingValidators = Array.Empty<HandlerMultiTargetMethod>();
                Throw.DebugAssert( _ambientServicesConfigurators == null );
                _ambientServicesConfigurators = Array.Empty<HandlerMultiTargetMethod>();
                Throw.DebugAssert( _handlingValidators == null );
                _handlingValidators = Array.Empty<HandlerMultiTargetMethod>();
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

        internal bool AddHandler( IActivityMonitor monitor,
                                  IStObjFinalClass owner,
                                  MethodInfo method,
                                  ParameterInfo[] parameters,
                                  ParameterInfo parameter,
                                  bool isClosedHandler,
                                  string? fileName,
                                  int lineNumber )
        {
            var (unwrappedReturnType, isRefAsync, isValAsync) = GetReturnParameterInfo( method );

            var expected = _commandResultType?.Type ?? typeof( void );

            if( unwrappedReturnType != expected )
            {
                using( monitor.TemporarilySetMinimalFilter( LogFilter.Trace ) )
                {
                    if( expected == typeof( void ) )
                    {
                        monitor.Warn( $"Handler method '{MethodName( method, parameters )}' must not return any value but returns a '{unwrappedReturnType.Name}'. This handler is skipped." );
                    }
                    else
                    {
                        monitor.Warn( $"Handler method '{MethodName( method, parameters )}': expected return type is '{expected.Name}', not '{unwrappedReturnType.Name}'. This handler is skipped." );
                    }
                    return true;
                }
            }
            if( _commandHandlerService != null && owner != _commandHandlerService )
            {
                monitor.Warn( $"Handler method '{MethodName( method, parameters )}' is skipped (the command handler is implemented by '{_commandHandlerService.ClassType:N}')." );
                return true;
            }
            if( _commandHandler != null )
            {
                if( _commandHandler.IsClosedHandler )
                {
                    if( isClosedHandler )
                    {
                        monitor.Error( $"Ambiguity: both '{MethodName( method, parameters )}' and '{_commandHandler}' handle '{PocoName}' command." );
                        return false;
                    }
                    WarnUnclosedHandlerSkipped( monitor, method, parameters );
                    return true;
                }
                // Current handler is an unclosed one.
                if( isClosedHandler )
                {
                    WarnUnclosedHandlerSkipped( monitor, _commandHandler.Method, _commandHandler.Parameters );
                }
                else
                {
                    // Two unclosed handlers. Should we use the two command Parameter types to decide?
                    // - c1 == c2 => Ambiguity.
                    // - c1 is assignable from c2 => c2
                    // - c2 is assignable from c1 => c1
                    // - c1 independent of c2 => Ambiguity.
                    monitor.Error( $"Ambiguity: both '{MethodName( method, parameters )}' and '{_commandHandler}' handle '{PocoName}' command." );
                    return false;
                }
            }
            _commandHandler = new HandlerMethod( this, owner, method, parameters, fileName, lineNumber, parameter, unwrappedReturnType, isRefAsync, isValAsync, isClosedHandler );
            CheckSyncAsyncMethodName( monitor, method, parameters, _commandHandler.IsRefAsync || _commandHandler.IsValAsync );
            return true;

            static void WarnUnclosedHandlerSkipped( IActivityMonitor monitor, MethodInfo method, ParameterInfo[] parameters )
            {
                monitor.Warn( $"Handler method '{MethodName( method, parameters )}' for unclosed command type is skipped since a closed handler is available." );
            }
        }

        internal bool AddMultiTargetHandler( IActivityMonitor monitor,
                                             MultiTargetHandlerKind target,
                                             IStObjFinalClass owner,
                                             MethodInfo method,
                                             ParameterInfo[] parameters,
                                             ParameterInfo cmdOrPartParameter,
                                             ParameterInfo? messageCollectorOrAmbientServiceHub,
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
            switch( target )
            {
                case MultiTargetHandlerKind.CommandIncomingValidator:
                case MultiTargetHandlerKind.CommandHandlingValidator:
                case MultiTargetHandlerKind.ConfigureAmbientServices:
                    Throw.DebugAssert( messageCollectorOrAmbientServiceHub != null );
                    var h = new HandlerMultiTargetMethod( this,
                                                          target switch
                                                          {
                                                              MultiTargetHandlerKind.CommandIncomingValidator => CrisHandlerKind.CommandIncomingValidator,
                                                              MultiTargetHandlerKind.CommandHandlingValidator => CrisHandlerKind.CommandHandlingValidator,
                                                              _ => CrisHandlerKind.ConfigureServices
                                                          },
                                                          owner,
                                                          method,
                                                          parameters,
                                                          fileName,
                                                          lineNumber,
                                                          cmdOrPartParameter,
                                                          messageCollectorOrAmbientServiceHub,
                                                          isRefAsync,
                                                          isValAsync );
                    if( target is MultiTargetHandlerKind.CommandIncomingValidator )
                    {
                        _incomingValidators ??= new List<HandlerMultiTargetMethod>();
                        _incomingValidators.Add( h );
                    }
                    else if( target is MultiTargetHandlerKind.CommandHandlingValidator )
                    {
                        _handlingValidators ??= new List<HandlerMultiTargetMethod>();
                        _handlingValidators.Add( h );
                    }
                    else
                    {
                        _ambientServicesConfigurators ??= new List<HandlerMultiTargetMethod>();
                        _ambientServicesConfigurators.Add( h );
                    }
                    break;

                case MultiTargetHandlerKind.RoutedEventHandler:
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

}
