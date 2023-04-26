using CK.CodeGen;
using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CK.Setup.Cris
{
    public partial class CommandRegistry
    {
        /// <summary>
        /// Command model.
        /// </summary>
        public partial class Entry
        {
            readonly List<ValidatorMethod> _validators;
            readonly List<PostHandlerMethod> _postHandlers;

            /// <summary>
            /// Gets the command root poco.
            /// </summary>
            public readonly IPocoRootInfo Command;

            /// <summary>
            /// Gets the handler method.
            /// </summary>
            public HandlerMethod? Handler { get; private set; }

            /// <summary>
            /// Gets the validator methods.
            /// </summary>
            public IReadOnlyList<ValidatorMethod> Validators => _validators;

            /// <summary>
            /// Gets the post handler methods.
            /// </summary>
            public IReadOnlyList<PostHandlerMethod> PostHandlers => _postHandlers;

            /// <summary>
            /// Gets the name of this command (this is the <see cref="IPocoRootInfo.Name"/>).
            /// </summary>
            public string CommandName => Command.Name;

            /// <summary>
            /// Gets the name of this command.
            /// </summary>
            public IReadOnlyList<string> PreviousNames => Command.PreviousNames;

            /// <summary>
            /// Gets a unique, zero-based index that identifies this command among all
            /// the <see cref="CommandRegistry.Commands"/>.
            /// </summary>
            public int CommandIdx { get; }

            /// <summary>
            /// Gets the final (most specialized) result type.
            /// This is <c>typeof(void)</c> when the command is a <see cref="ICommand"/> that
            /// is not a <see cref="ICommand{TResult}"/>.
            /// </summary>
            /// <remarks>
            /// This can be the special <c>typeof(NoWaitResult)</c> if the command is a
            /// fire &amp; forget command.
            /// </remarks>
            public Type ResultType => ResultNullableTypeTree.Type;

            /// <summary>
            /// Gets the final (most specialized) result nullable type tree.
            /// See <see cref="ResultType"/>.
            /// </remarks>
            public NullableTypeTree ResultNullableTypeTree { get; }

            /// <summary>
            /// Gets the <see cref="ResultType"/> as the <see cref="IPocoInterfaceInfo"/> if it is a IPoco.
            /// </summary>
            public IPocoInterfaceInfo? PocoResultType { get; }

            /// <summary>
            /// Gets whether there are asynchronous post handlers to call.
            /// </summary>
            /// <param name="e">The entry.</param>
            /// <returns>True if asynchronous calls must be made.</returns>
            public bool HasPostHandlerAsyncCall => _postHandlers.Any( h => h.IsRefAsync || h.IsValAsync );

            /// <summary>
            /// Gets the <see cref="IAutoService"/> that must implement the handler method.
            /// </summary>
            public IStObjFinalClass? ExpectedHandlerService { get; }

            /// <summary>
            /// Generates all the synchronous and asynchronous (if any) calls required based on these variable
            /// names: <code>IServiceProvider s, CK.Cris.KnownCommand c, object r</code> and the variables
            /// in <paramref name="cachedServices"/>.
            /// <para>
            /// Async calls use await keyword.
            /// </para>
            /// </summary>
            /// <param name="w">The code writer to use.</param>
            /// <param name="cachedServices">GetService cache.</param>
            public void GeneratePostHandlerCallCode( ICodeWriter w, VariableCachedServices cachedServices )
            {
                w.GeneratedByComment().NewLine();
                foreach( var h in _postHandlers.Where( h => !h.IsRefAsync && !h.IsValAsync ).GroupBy( h => h.Owner ) )
                {
                    GenerateCode( w, h, false, cachedServices );
                }
                foreach( var h in _postHandlers.Where( h => h.IsRefAsync || h.IsValAsync ).GroupBy( h => h.Owner ) )
                {
                    GenerateCode( w, h, true, cachedServices );
                }

                static void GenerateCode( ICodeWriter w,
                                          IGrouping<IStObjFinalClass, PostHandlerMethod> h,
                                          bool async,
                                          VariableCachedServices cachedServices )
                {
                    w.Append( "{" ).NewLine();
                    w.Append( "var h = (" ).Append( h.Key.ClassType.ToCSharpName() ).Append( ")s.GetService(" ).AppendTypeOf( h.Key.ClassType ).Append( ");" ).NewLine();
                    foreach( PostHandlerMethod m in h )
                    {
                        if( async ) w.Append( "await " );

                        if( m.Method.DeclaringType != h.Key.ClassType )
                        {
                            w.Append( "((" ).Append( m.Method.DeclaringType!.ToCSharpName() ).Append( ")h)." );
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
                                    w.Append( "(" ).Append( p.ParameterType.ToCSharpName() ).Append( ")" );
                                }
                                w.Append( "r" );
                            }
                            else if( p == m.CmdOrPartParameter )
                            {
                                w.Append( "(" ).Append( m.CmdOrPartParameter.ParameterType.ToCSharpName() ).Append( ")c" );
                            }
                            else
                            {
                                cachedServices.WriteGetService( w, p.ParameterType );
                            }
                        }
                        w.Append( " );" ).NewLine();
                    }
                    w.Append( "}" ).NewLine();
                }
            }

            /// <summary>
            /// Overridden to return the <see cref="CommandName"/>.
            /// </summary>
            /// <returns>The name of this command.</returns>
            public override string ToString() => CommandName;

            Entry( IPocoRootInfo command,
                   int commandIdx,
                   NullableTypeTree resultType,
                   IPocoInterfaceInfo? pocoResultType,
                   IStObjFinalClass? handlerService )
            {
                Command = command;
                _validators = new List<ValidatorMethod>();
                _postHandlers = new List<PostHandlerMethod>();
                CommandIdx = commandIdx;
                ResultNullableTypeTree = resultType;
                PocoResultType = pocoResultType;
                ExpectedHandlerService = handlerService;
            }

            internal static Entry? Create( IActivityMonitor monitor, IPocoSupportResult pocoSupportResult, IPocoRootInfo command, int commandIdx, IStObjFinalClass? handlerService )
            {
                Type resultType;
                IPocoInterfaceInfo? pocoResultType;

                #region Handling TResult
                static Type? ExtractTResult( Type i )
                {
                    if( !i.IsGenericType ) return null;
                    Type tG = i.GetGenericTypeDefinition();
                    if( tG != typeof( ICommand<> ) ) return null;
                    return i.GetGenericArguments()[0];
                }

                static PropertyInfo? ExtractResultInfo( Type i )
                {
                    if( !i.IsGenericType ) return null;
                    Type tG = i.GetGenericTypeDefinition();
                    if( tG != typeof( ICommand<> ) ) return null;
                    return i.GetProperty( "R", BindingFlags.Static | BindingFlags.NonPublic )!;
                }

                var resultTypes = command.OtherInterfaces.Select( i => ExtractTResult( i ) )
                                                    .Where( r => r != null )
                                                    .Select( t => t! )
                                                    .ToList();
                if( resultTypes.Count > 0 )
                {
                    for( int i = resultTypes.Count - 2; i >= 0; --i )
                    {
                        var x = resultTypes[resultTypes.Count - 1];
                        var y = resultTypes[i];
                        if( x.IsAssignableFrom( y ) )
                        {
                            resultTypes.RemoveAt( resultTypes.Count - 1 );
                            i = resultTypes.Count - 1;
                        }
                        else if( y.IsAssignableFrom( x ) )
                        {
                            resultTypes.RemoveAt( i );
                        }
                    }
                    if( resultTypes.Count == 1 )
                    {
                        resultType = resultTypes[0]!;
                        pocoResultType = pocoSupportResult.Find( resultType );
                    }
                    else
                    {
                        monitor.Error( $"Invalid command Result type for '{command.Name}': result types '{resultTypes.Select( t => t.Name ).Concatenate( "', '" )}' must resolve to a common most specific type." );
                        return null;
                    }
                }
                else
                {
                    resultType = typeof( void );
                    pocoResultType = null;
                }
                #endregion

                return new Entry( command, commandIdx, resultType.GetNullableTypeTree(), pocoResultType, handlerService );
            }

            internal bool AddHandler( IActivityMonitor monitor,
                                      IStObjFinalClass owner,
                                      MethodInfo method,
                                      ParameterInfo[] parameters,
                                      ParameterInfo parameter,
                                      bool isClosedHandler )
            {
                // If the Command is closed, we silently skip handlers of unclosed commands: we expect the final handler of the closure interface.
                if( !isClosedHandler && Command.ClosureInterface != null )
                {
                    monitor.Info( $"Method {MethodName( method, parameters )} cannot handle '{CommandName}' command because type {parameter.ParameterType.Name} doesn't represent the whole command." );
                    return true;
                }

                var (unwrappedReturnType, isRefAsync, isValAsync) = GetReturnParameterInfo( method );

                var expected = ResultType;
                if( expected == typeof( ICrisEvent.NoWaitResult ) ) expected = typeof( void );

                if( unwrappedReturnType != expected )
                {
                    using( monitor.TemporarilySetMinimalFilter( LogFilter.Trace ) )
                    {
                        if( expected == typeof( void ) )
                        {
                            monitor.Warn( $"Handler method '{MethodName( method, parameters )}' must not return any value, not '{unwrappedReturnType.Name}'. This handler is skipped." );
                        }
                        else
                        {
                            monitor.Warn( $"Handler method '{MethodName( method, parameters )}': expected return type is '{expected.Name}', not '{unwrappedReturnType.Name}'. This handler is skipped." );
                        }
                        return true;
                    }
                }
                if( ExpectedHandlerService != null && owner != ExpectedHandlerService )
                {
                    monitor.Warn( $"Handler method '{MethodName( method, parameters )}' is skipped (the command handler is implemented by '{ExpectedHandlerService.ClassType.FullName}')." );
                    return true;
                }
                if( Handler != null )
                {
                    if( Handler.IsClosedHandler )
                    {
                        if( isClosedHandler )
                        {
                            monitor.Error( $"Ambiguity: both '{MethodName( method, parameters )}' and '{Handler}' handle '{CommandName}' command." );
                            return false;
                        }
                        WarnUnclosedHandlerSkipped( monitor, method, parameters );
                        return true;
                    }
                    // Current handler is an unclosed one.
                    if( isClosedHandler )
                    {
                        WarnUnclosedHandlerSkipped( monitor, Handler.Method, Handler.Parameters );
                    }
                    else
                    {
                        // Two unclosed handlers. Should we use the two command Parameter types?
                        // - c1 == c2 => Ambiguity.
                        // - c1 is assignable form c2 => c2
                        // - c2 is assignable from c1 => c1
                        // - c1 independent of c2 => Ambiguity.
                        monitor.Error( $"Ambiguity: both '{MethodName( method, parameters )}' and '{Handler}' handle '{CommandName}' command." );
                        return false;
                    }
                }
                Handler = new HandlerMethod( this, owner, method, parameters, parameter, unwrappedReturnType, isRefAsync, isValAsync, isClosedHandler );
                CheckSyncAsyncMethodName( monitor, method, parameters, Handler.IsRefAsync || Handler.IsValAsync );
                return true;

                static void WarnUnclosedHandlerSkipped( IActivityMonitor monitor, MethodInfo method, ParameterInfo[] parameters )
                {
                    monitor.Warn( $"Handler method '{MethodName( method, parameters )}' for unclosed command type is skipped since a closed handler is available." );
                }
            }

            internal bool AddValidator( IActivityMonitor monitor, IStObjFinalClass owner, MethodInfo method, ParameterInfo[] parameters, ParameterInfo commandParameter )
            {
                if( !CheckVoidReturn( monitor, "Validator", method, parameters, out bool isRefAsync, out bool isValAsync ) ) return false;
                _validators.Add( new ValidatorMethod( this, owner, method, parameters, commandParameter, isRefAsync, isValAsync ) );
                return true;
            }

            internal bool AddPostHandler( IActivityMonitor monitor, IStObjFinalClass owner, MethodInfo method, ParameterInfo[] parameters, ParameterInfo commandParameter )
            {
                if( !CheckVoidReturn( monitor, "PostHandler", method, parameters, out bool isRefAsync, out bool isValAsync ) ) return false;

                // Looking for the command result in the parameters.
                bool mustCastResultParameter = false;
                ParameterInfo? resultParameter = null;
                if( PocoResultType != null )
                {
                    // The result is a IPoco: the first parameter that is one of the IPoco interfaces or a non IPoco interface (like a definer)
                    // that this poco supports is fine.
                    resultParameter = parameters.FirstOrDefault( p => p.ParameterType is Type t
                                                                      && (PocoResultType.Root.Interfaces.Any( itf => itf.PocoInterface == t )
                                                                          || PocoResultType.Root.OtherInterfaces.Contains( t )) );
                    if( resultParameter != null ) mustCastResultParameter = true;
                }
                else if( ResultType != typeof( void ) && ResultType != typeof( ICrisEvent.NoWaitResult ) )
                {
                    // The result type is not a IPoco. The first parameter that can be assigned to the result type is fine. 
                    resultParameter = parameters.FirstOrDefault( p => p.ParameterType.IsAssignableFrom( ResultType ) );
                }
                if( resultParameter != null )
                {
                    monitor.Trace( $"PostHandler method '{MethodName( method, parameters )}': parameter '{resultParameter.Name}' is the Command's result." );
                }
                _postHandlers.Add( new PostHandlerMethod( this,
                                                          owner,
                                                          method,
                                                          parameters,
                                                          commandParameter,
                                                          resultParameter,
                                                          mustCastResultParameter,
                                                          isRefAsync,
                                                          isValAsync ) );
                return true;
            }

            static bool CheckVoidReturn( IActivityMonitor monitor, string kind, MethodInfo method, ParameterInfo[] parameters, out bool isRefAsync, out bool isValAsync )
            {
                Type unwrappedReturnType;
                (unwrappedReturnType, isRefAsync, isValAsync) = GetReturnParameterInfo( method );
                if( unwrappedReturnType != typeof( void ) )
                {
                    monitor.Error( $"{kind} method '{MethodName( method, parameters )}' must not return any value. Its returned type is '{unwrappedReturnType.Name}'." );
                    return false;
                }
                CheckSyncAsyncMethodName( monitor, method, parameters, isRefAsync || isValAsync );
                return true;
            }
        }
    }

}
