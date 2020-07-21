using CK.Core;
using CK.Cris;
using CK.Text;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Holds the set of <see cref="Entry"/> that have been discovered.
    /// </summary>
    public partial class CommandRegistry
    {
        readonly IPocoSupportResult _pocoResult;
        readonly IReadOnlyDictionary<IPocoRootInfo, Entry> _indexedCommands;

        /// <summary>
        /// Gets all the discovered commands ordered by their <see cref="Entry.CommandIdx"/>.
        /// </summary>
        public IReadOnlyList<Entry> Commands { get; }


        internal bool RegisterHandler( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m, bool allowUnclosed )
        {
            (Entry? e, ParameterInfo[]? parameters, ParameterInfo? p) = GetCommandEntry( monitor, "Handler", m );
            if( e == null ) return false;
            Debug.Assert( parameters != null && p != null );
            bool isClosedHandler = p.ParameterType == e.Command.ClosureInterface;
            if( !isClosedHandler && !allowUnclosed )
            {
                allowUnclosed = p.GetCustomAttributes().Any( a => a.GetType().FindInterfaces( (i,n) => i.Name == (string?)n, "IAllowUnclosedCommandAttribute" ).Length > 0 );
                if( !allowUnclosed )
                {
                    monitor.Info( $"Method {MethodName( m, parameters )} cannot handle '{e.CommandName}' command because type {p.ParameterType.Name} doesn't represent the whole command." );
                    return true;
                }
            }
            return e.AddHandler( monitor, impl, m, parameters, p, isClosedHandler );
        }

        internal bool RegisterPostHandler( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m )
        {
            (Entry? e, ParameterInfo[]? parameters, ParameterInfo? p) = GetCommandEntry( monitor, "PostHandler", m );
            if( e == null ) return false;
            Debug.Assert( parameters != null && p != null );
            return e.AddPostHandler( monitor, impl, m, parameters, p );
        }

        internal bool RegisterValidator( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m )
        {
            (ParameterInfo[]? parameters, ParameterInfo? p, IReadOnlyList<IPocoRootInfo>? commands) = GetImpactedCommands( monitor, impl, m );
            if( p != null )
            {
                Debug.Assert( parameters != null );
                bool success = true;
                Debug.Assert( commands != null, "p == null <==> commands == null" );
                foreach( var command in commands )
                {
                    Debug.Assert( _indexedCommands.ContainsKey( command ), "Since parameters are filtered by registered Poco." );
                    var e = _indexedCommands[command];
                    success &= e.AddValidator( monitor, impl, m, parameters, p );
                }
                return success;
            }
            return false;
        }


        (ParameterInfo[]? Parameters, ParameterInfo? Param, IReadOnlyList<IPocoRootInfo>? Commands) GetImpactedCommands( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m )
        {
            (ParameterInfo[]? parameters, ParameterInfo? p, IPocoInterfaceInfo? commandInterface) = GetCommandCandidate( monitor, m );
            if( parameters == null ) return (null, null, null);
            if( p != null )
            {
                Debug.Assert( commandInterface != null, "Since we have a ICommand parameter." );
                return (parameters, p, new IPocoRootInfo[] { commandInterface.Root });
            }
            // Looking for command parts.
            var (param, commands) = GetCommandsFromPart( monitor, m, parameters );
            if( param != null )
            {
                Debug.Assert( commands != null, "param == null <==> commands == null" );
                return (parameters, param, commands);
            }
            return (parameters, null, null);
        }

        (ParameterInfo? Parameter, IReadOnlyList<IPocoRootInfo>? Commands) GetCommandsFromPart( IActivityMonitor monitor, MethodInfo m, ParameterInfo[] parameters )
        {
            var candidates = parameters.Where( param => typeof( ICommandPart ).IsAssignableFrom( param.ParameterType ) )
                                        .Select( param => (param, commandPartsList: _pocoResult.OtherInterfaces.GetValueOrDefault( param.ParameterType )) )
                                        .Where( x => x.commandPartsList != null )
                                        .ToArray();
            if( candidates.Length > 1 )
            {
                monitor.Error( $"Method {MethodName( m, parameters )} cannot have more than one ICommand or ICommandPart parameter." );
            }
            else if( candidates.Length == 0 )
            {
                monitor.Error( $"Method {MethodName( m, parameters )}: no ICommand nor ICommandPart parameter detected." );
            }
            else
            {
                Debug.Assert( candidates[0].commandPartsList != null, "Nulls have been filtered out." );
                return (candidates[0].param, candidates[0].commandPartsList);
            }
            return (null, null);
        }

        /// <summary>
        /// Gets or builds a <see cref="CommandRegistry"/> for a <see cref="ICodeGenerationContext"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="c">The context.</param>
        /// <returns>The directory or null on error.</returns>
        public static CommandRegistry? FindOrCreate( IActivityMonitor monitor, ICodeGenerationContext c )
        {
            if( !c.Assembly.Memory.TryGetCachedInstance<CommandRegistry>( out var result ) )
            {
                IPocoSupportResult pocoResult = c.Assembly.GetPocoSupportResult();
                var (index,commands) = CreateCommandMap( monitor, c.CurrentRun.EngineMap, pocoResult );
                if( commands != null )
                {
                    Debug.Assert( index != null, "Since commands is not null." );
                    monitor.Info( commands.Count > 0 ? $"{commands.Count} commands detected." : "No Command detected." );
                    result = new CommandRegistry( index, commands, pocoResult );
                }
                c.Assembly.Memory.AddCachedInstance( result );
            }
            return result;
        }


        CommandRegistry( IReadOnlyDictionary<IPocoRootInfo, Entry> index, IReadOnlyList<Entry> commands, IPocoSupportResult poco )
        {
            _indexedCommands = index;
            Commands = commands;
            _pocoResult = poco;
        }

        static (Dictionary<IPocoRootInfo, Entry>?,IReadOnlyList<Entry>?) CreateCommandMap( IActivityMonitor monitor, IStObjEngineMap services, IPocoSupportResult pocoResult )
        {
            bool success = true;
            var index = new Dictionary<IPocoRootInfo, Entry>();
            var commands = new List<Entry>();
            if( pocoResult.OtherInterfaces.TryGetValue( typeof( ICommand ), out IReadOnlyList<IPocoRootInfo>? commandPocos ) )
            {
                foreach( var poco in commandPocos )
                {
                    var hServices = poco.Interfaces.Select( i => typeof( ICommandHandler<> ).MakeGenericType( i.PocoInterface ) )
                                                        .Select( gI => (itf: gI, impl: services.Find( gI )) )
                                                        .Where( m => m.impl != null )
                                                        .GroupBy( m => m.impl )
                                                        .ToArray();
                    if( hServices.Length > 1 )
                    {
                        monitor.Error( $"Ambiguous command handler '{hServices.Select( m => $"{m.Key.ClassType.FullName}' implements '{m.Select( x => x.itf.FullName ).Concatenate( "' ,'" )}" )}': only one service can eventually handle a command." );
                        success = false;
                    }
                    var entry = Entry.Create( monitor, pocoResult, poco, commands.Count, hServices.Length == 1 ? hServices[0].Key : null ) ;
                    if( entry == null ) success = false;
                    else
                    {
                        commands.Add( entry );
                        index.Add( entry.Command, entry );
                    }
                }
            }
            return success ? (index, commands) : (null, null);
        }

        static string MethodName( MethodInfo m, ParameterInfo[]? parameters = null ) => $"{m.DeclaringType!.Name}.{m.Name}( {(parameters ?? m.GetParameters()).Select( p => p.ParameterType.Name + " " + p.Name ).Concatenate()} )";

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


        (Entry? e, ParameterInfo[]? parameters, ParameterInfo? p) GetCommandEntry( IActivityMonitor monitor, string kind, MethodInfo m )
        {
            (ParameterInfo[]? parameters, ParameterInfo? p, IPocoInterfaceInfo? commandInterface) = GetCommandCandidate( monitor, m );
            if( parameters != null )
            {
                if( p == null )
                {
                    monitor.Error( $"Invalid {kind} method {MethodName( m, parameters )} skipped. No ICommand parameter detected." );
                }
                else
                {
                    Debug.Assert( commandInterface != null, "Since we have a parameter." );
                    Debug.Assert( parameters != null );
                    Debug.Assert( _indexedCommands.ContainsKey( commandInterface.Root ), "Since parameters are filtered by registered Poco." );
                    return (_indexedCommands[commandInterface.Root], parameters, p);
                }
            }
            return (null, null, null);
        }

        (ParameterInfo[]? Parameters, ParameterInfo? Param, IPocoInterfaceInfo? ParamInterface) GetCommandCandidate( IActivityMonitor monitor, MethodInfo m )
        {
            var parameters = m.GetParameters();
            var candidates = parameters.Select( p => (p, _pocoResult.AllInterfaces.GetValueOrDefault( p.ParameterType )) )
                                                                    .Where( x => x.Item2 != null
                                                                                && typeof( ICommand ).IsAssignableFrom( x.Item2.PocoInterface ) )
                                                                    .ToArray();
            if( candidates.Length > 1 )
            {
                monitor.Error( $"Method {MethodName( m, parameters )} cannot have more than one ICommand parameter." );
                return (null, null, null);
            }
            if( candidates.Length == 0 )
            {
                return (parameters, null, null);
            }
            Debug.Assert( candidates[0].Item2 != null && candidates[0].p.ParameterType == candidates[0].Item2!.PocoInterface );
            return (parameters, candidates[0].p, candidates[0].Item2);
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
