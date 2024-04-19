using CK.CodeGen;
using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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

        readonly List<HandlerRoutedEventMethod> _eventHandlers;

        readonly IPocoType? _commandResultType;
        readonly List<HandlerValidatorMethod> _validators;
        readonly List<HandlerValidatorMethod> _syntaxValidators;
        readonly List<HandlerPostMethod> _postHandlers;
        readonly IStObjFinalClass? _commandHandlerService;
        HandlerMethod? _commandHandler;

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
        public bool IsHandled => _commandHandler != null || _eventHandlers.Count > 0;

        /// <summary>
        /// Gets the syntax validator methods.
        /// Only <see cref="CrisPocoKind.Command"/> and <see cref="CrisPocoKind.CommandWithResult"/> can have validators.
        /// </summary>
        public IReadOnlyList<HandlerValidatorMethod> SyntaxValidators => _syntaxValidators;

        /// <summary>
        /// Gets the validator methods.
        /// Only <see cref="CrisPocoKind.Command"/> and <see cref="CrisPocoKind.CommandWithResult"/> can have validators.
        /// </summary>
        public IReadOnlyList<HandlerValidatorMethod> Validators => _validators;

        /// <summary>
        /// Gets the post handler methods.
        /// Only <see cref="CrisPocoKind.Command"/> and <see cref="CrisPocoKind.CommandWithResult"/> can have post handlers.
        /// </summary>
        public IReadOnlyList<HandlerPostMethod> PostHandlers => _postHandlers;

        /// <summary>
        /// Gets the event handlers methods.
        /// Only <see cref="CrisPocoKind.RoutedImmediateEvent"/> and <see cref="CrisPocoKind.RoutedEvent"/> can have event handlers.
        /// </summary>
        public IReadOnlyList<HandlerRoutedEventMethod> EventHandlers => _eventHandlers;

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
        public bool HasPostHandlerAsyncCall => _postHandlers.Any( h => h.IsRefAsync || h.IsValAsync );

        /// <summary>
        /// Gets the <see cref="IAutoService"/> that must implement the handler method.
        /// </summary>
        public IStObjFinalClass? ExpectedHandlerService => _commandHandlerService;

        /// <summary>
        /// Generates all the synchronous and asynchronous (if any) calls required based on these variable
        /// names: <code>IServiceProvider s, ICrisPoco c, object r</code> and the variables
        /// in <paramref name="cachedServices"/>.
        /// <para>
        /// Async calls use await keyword.
        /// </para>
        /// </summary>
        /// <param name="w">The code writer to use.</param>
        /// <param name="cachedServices">GetService cache.</param>
        public void GeneratePostHandlerCallCode( ICodeWriter w, VariableCachedServices cachedServices )
        {
            using var region = w.Region();
            foreach( var h in _postHandlers.Where( h => !h.IsRefAsync && !h.IsValAsync ).GroupBy( h => h.Owner ) )
            {
                CreateOwnerCalls( w, h, false, cachedServices );
            }
            foreach( var h in _postHandlers.Where( h => h.IsRefAsync || h.IsValAsync ).GroupBy( h => h.Owner ) )
            {
                CreateOwnerCalls( w, h, true, cachedServices );
            }

            static void CreateOwnerCalls( ICodeWriter w,
                                          IGrouping<IStObjFinalClass, HandlerPostMethod> oH,
                                          bool async,
                                          VariableCachedServices cachedServices )
            {
                w.OpenBlock()
                 .Append( "var h = (" ).Append( oH.Key.ClassType.ToGlobalTypeName() ).Append( ")s.GetService(" ).AppendTypeOf( oH.Key.ClassType ).Append( ");" ).NewLine();
                foreach( HandlerPostMethod m in oH )
                {
                    if( async ) w.Append( "await " );

                    if( m.Method.DeclaringType != oH.Key.ClassType )
                    {
                        w.Append( "((" ).Append( m.Method.DeclaringType!.ToGlobalTypeName() ).Append( ")h)." );
                    }
                    else w.Append( "h." );

                    w.Append( m.Method.Name ).Append( "( " );
                    foreach( ParameterInfo p in m.Parameters )
                    {
                        if( p.Position > 0 ) w.Append( ", " );
                        if( p == m.ResultParameter )
                        {
                            if( m.MustCastResultParameter )
                            {
                                w.Append( "(" ).AppendGlobalTypeName( p.ParameterType ).Append( ")" );
                            }
                            w.Append( "r" );
                        }
                        else if( p == m.CmdOrPartParameter )
                        {
                            w.Append( "(" ).AppendGlobalTypeName( m.CmdOrPartParameter.ParameterType ).Append( ")c" );
                        }
                        else
                        {
                            w.Append( cachedServices.GetServiceVariableName( p.ParameterType ) );
                        }
                    }
                    w.Append( " );" ).NewLine();
                }
                w.CloseBlock();
            }
        }

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
            _syntaxValidators = new List<HandlerValidatorMethod>();
            _validators = new List<HandlerValidatorMethod>();
            _postHandlers = new List<HandlerPostMethod>();
            _eventHandlers = new List<HandlerRoutedEventMethod>();
            _commandHandlerService = handlerService;
            _kind = kind;
            _crisPocoIndex = crisPocoIdx;
            _commandResultType = resultType;
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
            // If the Command is closed, we silently skip handlers of unclosed commands: we expect the final handler of the closure interface.
            if( !isClosedHandler && _crisPocoType.FamilyInfo.ClosureInterface != null )
            {
                monitor.Info( $"Method {MethodName( method, parameters )} cannot handle '{PocoName}' command because type {parameter.ParameterType.Name} doesn't represent the whole command." );
                return true;
            }

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

        internal bool AddValidator( IActivityMonitor monitor,
                                    bool isSyntax,
                                    IStObjFinalClass owner,
                                    MethodInfo method,
                                    ParameterInfo[] parameters,
                                    ParameterInfo commandParameter,
                                    ParameterInfo validationContextParameter,
                                    string? fileName,
                                    int lineNumber )
        {
            if( !CheckVoidReturn( monitor, "Validator", method, parameters, out bool isRefAsync, out bool isValAsync ) ) return false;
            var h = new HandlerValidatorMethod( this, isSyntax, owner, method, parameters, fileName, lineNumber, commandParameter, validationContextParameter, isRefAsync, isValAsync );
            (isSyntax ? _syntaxValidators : _validators ).Add( h );
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
            if( !CheckVoidReturn( monitor, "PostHandler", method, parameters, out bool isRefAsync, out bool isValAsync ) ) return false;

            // Looking for the command result in the parameters.
            bool mustCastResultParameter = false;
            ParameterInfo? resultParameter = null;
            if( _commandResultType != null )
            {
                // Elects the first parameter that is compatible with the result type.
                resultParameter = parameters.FirstOrDefault( p => typeSystem.FindByType( p.ParameterType )?.CanReadFrom( _commandResultType ) ?? false );
                if( resultParameter != null ) mustCastResultParameter = resultParameter.ParameterType != _commandResultType.Type;
            }
            if( resultParameter != null )
            {
                monitor.Trace( $"PostHandler method '{MethodName( method, parameters )}': parameter '{resultParameter.Name}' is the Command's result." );
            }
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

        internal bool AddRoutedEventHandler( IActivityMonitor monitor,
                                             IStObjFinalClass owner,
                                             MethodInfo method,
                                             ParameterInfo[] parameters,
                                             ParameterInfo commandParameter,
                                             string? fileName,
                                             int lineNumber )
        {
            if( !CheckVoidReturn( monitor, "EventHandler", method, parameters, out bool isRefAsync, out bool isValAsync ) ) return false;
            if( _kind != CrisPocoKind.RoutedImmediateEvent && _kind != CrisPocoKind.RoutedEvent )
            {
                monitor.Warn( $"Method '{MethodName( method, parameters )}' will never be called: event '{PocoName}' is not decorated with [RoutedEvent] attribute (it can only be observed by the caller)." );
            }
            else
            {
                _eventHandlers.Add( new HandlerRoutedEventMethod( this, owner, method, parameters, fileName, lineNumber, commandParameter, isRefAsync, isValAsync ) );
            }
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

        static bool CheckVoidReturn( IActivityMonitor monitor, string kind, MethodInfo method, ParameterInfo[] parameters, out bool isRefAsync, out bool isValAsync )
        {
            Type unwrappedReturnType;
            (unwrappedReturnType, isRefAsync, isValAsync) = GetReturnParameterInfo( method );
            if( unwrappedReturnType != typeof( void ) )
            {
                monitor.Error( $"{kind} method '{MethodName( method, parameters )}' must not return any value. Its current returned type is '{unwrappedReturnType.Name}'." );
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
