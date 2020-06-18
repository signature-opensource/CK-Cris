using CK.Core;
using CK.Cris;
using CK.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        public class Entry
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
            /// Gets the post hanlder methods.
            /// </summary>
            public IReadOnlyList<PostHandlerMethod> PostHandlers => _postHandlers;

            /// <summary>
            /// Gets the name of this command.
            /// </summary>
            public string CommandName { get; }

            /// <summary>
            /// Gets the name of this command.
            /// </summary>
            public IReadOnlyList<string> PreviousNames { get; }

            /// <summary>
            /// Gets a unique, zero-based index that identifies this command among all
            /// the <see cref="CommandRegistry.Commands"/>.
            /// </summary>
            public int CommandIdx { get; }

            /// <summary>
            /// Gets the final (most specialized) result type.
            /// </summary>
            public Type? ResultType { get; }

            /// <summary>
            /// Gets the <see cref="ResultType"/> as the <see cref="IPocoInterfaceInfo"/> if it is a IPoco:
            /// if not null, then the <see cref="IPocoRootInfo.ClosureInterface"/> is neceessarily the <see cref="ResultType"/>.
            /// </summary>
            public IPocoInterfaceInfo? PocoResultType { get; }

            /// <summary>
            /// Overridden to return the <see cref="CommandName"/>.
            /// </summary>
            /// <returns>The name of this command.</returns>
            public override string ToString() => CommandName;

            Entry( IPocoRootInfo command, string name, string[] previousNames, int commandIdx, Type? resultType, IPocoInterfaceInfo? pocoResultType )
            {
                Command = command;
                _validators = new List<ValidatorMethod>();
                _postHandlers = new List<PostHandlerMethod>();
                Debug.Assert( Command.ClosureInterface != null );
                CommandName = name;
                PreviousNames = previousNames;
                CommandIdx = commandIdx;
                ResultType = resultType;
                PocoResultType = pocoResultType;
            }

            internal static Entry? Create( IActivityMonitor monitor, IPocoSupportResult pocoSupportResult, IPocoRootInfo command, int commandIdx )
            {
                string name;
                string[] previousNames;

                #region Handling CommandName attributes. 
                var names = command.PrimaryInterface.GetCustomAttributesData().Where( d => typeof( CommandNameAttribute ).IsAssignableFrom( d.AttributeType ) ).FirstOrDefault();

                var others = command.Interfaces.Where( i => i.PocoInterface != command.PrimaryInterface
                                                            && i.PocoInterface.GetCustomAttributesData().Any( x => typeof( CommandNameAttribute ).IsAssignableFrom( x.AttributeType ) ) );
                if( others.Any() )
                {
                    monitor.Error( $"CommandName attribute appear on '{others.Select( i => i.PocoInterface.FullName ).Concatenate("', '")}'. Only the primary ICommand interface (i.e. '{command.PrimaryInterface.FullName}') should define the Command names." );
                    return null;
                }
                if( names != null )
                {
                    var args = names.ConstructorArguments;
                    name = (string)args[0].Value!;
                    previousNames = ((IEnumerable<CustomAttributeTypedArgument>)args[1].Value!).Select( a => (string)a.Value! ).ToArray();
                    if( String.IsNullOrWhiteSpace( name ) )
                    {
                        monitor.Error( $"Empty name in CommandName attribute on '{command.PrimaryInterface.FullName}'." );
                        return null;
                    }
                    if( previousNames.Any( n => String.IsNullOrWhiteSpace( n ) ) )
                    {
                        monitor.Error( $"Empty previous name in CommandName attribute on '{command.PrimaryInterface.FullName}'." );
                        return null;
                    }
                    if( previousNames.Contains( name ) || previousNames.GroupBy( Util.FuncIdentity ).Any( g => g.Count() > 1 ) )
                    {
                        monitor.Error( $"Duplicate CommandName in attribute on '{command.PrimaryInterface.FullName}'." );
                        return null;
                    }
                }
                else
                {
                    name = command.PrimaryInterface.FullName!;
                    previousNames = Array.Empty<string>();
                    monitor.Warn( $"Command '{name}' use its full name as its name since no CommandName attribute is defined." );
                }
                #endregion

                Type? resultType;
                IPocoInterfaceInfo? pocoResultType;

                #region Handling TResult
                static Type? ExtractTResult( Type i )
                {
                    if( !i.IsGenericType ) return null;
                    Type tG = i.GetGenericTypeDefinition();
                    if( tG != typeof( ICommand<> ) ) return null;
                    return i.GetGenericArguments()[0];
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
                        monitor.Error( $"Invalid command Result type for '{name}': result types '{resultTypes.Select( t => t.Name ).Concatenate( "', '" )}' must resolve to a common most specific type." );
                        return null;
                    }
                }
                else
                {
                    resultType = null;
                    pocoResultType = null;
                } 
                #endregion

                return new Entry( command, name, previousNames, commandIdx, resultType, pocoResultType );
            }

            internal void AddUnclosedHandler( IActivityMonitor monitor, MethodInfo method, ParameterInfo[] parameters, ParameterInfo parameter, IPocoInterfaceInfo iCommand )
            {
                monitor.Info( $"Method {MethodName( method, parameters )} cannot handle '{CommandName}' command because type {iCommand.PocoInterface.Name} doesn't represent the whole command." );
            }

            internal bool AddHandler( IActivityMonitor monitor, IStObjFinalClass owner, MethodInfo method, ParameterInfo[] parameters, ParameterInfo parameter )
            {
                if( Handler != null )
                {
                    monitor.Error( $"Ambiguity: both '{MethodName( method, parameters )}' and '{Handler}' handle '{CommandName}' command." );
                    return false;
                }
                Handler = new HandlerMethod( this, owner, method, parameters, parameter );
                CheckSyncAsyncMethodName( monitor, method, parameters, Handler.IsRefAsync || Handler.IsValAsync );
                return true;
            }

            internal bool AddValidator( IActivityMonitor monitor, IStObjFinalClass owner, MethodInfo method, ParameterInfo[] parameters, ParameterInfo commandParameter )
            {
                var (unwrappedReturnType, isRefAsync, isValAsync) = GetReturnParameterInfo( method );
                if( unwrappedReturnType != typeof(void) )
                {
                    monitor.Error( $"Validate method '{MethodName( method, parameters )}' must not return any value (void, Task or ValueTask). Its returned type is '{unwrappedReturnType.Name}'." );
                    return false;
                }
                CheckSyncAsyncMethodName( monitor, method, parameters, isRefAsync || isValAsync );
                _validators.Add( new ValidatorMethod( this, owner, method, parameters, commandParameter, isRefAsync, isValAsync ) );
                return true;
            }

            internal bool AddPostHandler( IActivityMonitor monitor, IStObjFinalClass owner, MethodInfo method, ParameterInfo[] parameters, ParameterInfo commandParameter )
            {
                return true;
            }
        }

    }

}
