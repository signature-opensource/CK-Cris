using CK.Core;
using CK.Cris;
using CK.Setup;
using CK.Text;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Holds the set of <see cref="Entry"/> that have been discovered.
    /// </summary>
    public partial class CommandRegistry
    {
        readonly IPocoSupportResult _pocoResult;

        /// <summary>
        /// Gets the discovered commands indexed by their <see cref="Entry.CommandName"/>, <see cref="Entry.PreviousNames"/> and
        /// the <see cref="IPocoRootInfo"/>.
        /// </summary>
        public IReadOnlyDictionary<object, Entry> IndexedCommands { get; }

        /// <summary>
        /// Gets all the discovered commands.
        /// </summary>
        public IReadOnlyList<Entry> Commands { get; }


        internal bool RegisterHandler( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m )
        {
            (ParameterInfo[]? parameters, ParameterInfo? p, IPocoInterfaceInfo? commandInterface) = GetCommandCandidates( monitor, m );
            bool success = parameters != null;
            if( success )
            {
                if( p == null )
                {
                    monitor.Error( $"Method {MethodName( m, parameters )} skipped. No ICommand parameter detected." );
                    success = false;
                }
                else
                {
                    Debug.Assert( commandInterface != null, "Since we have a parameter." );
                    Debug.Assert( parameters != null );
                    Debug.Assert( commandInterface.Root.ClosureInterface != null, "Since this a ICommand : IClosedPoco." );
                    Debug.Assert( IndexedCommands.ContainsKey( commandInterface.Root ), "Since parameters are filtered by registered Poco." );
                    Entry cmd = IndexedCommands[commandInterface.Root];
                    if( commandInterface.PocoInterface != commandInterface.Root.ClosureInterface )
                    {
                        cmd.AddUnclosedHandler( monitor, m, parameters, p, commandInterface );
                    }
                    else
                    {
                        success &= cmd.AddHandler( monitor, impl, m, parameters, p );
                    }
                }
            }
            return success;
        }

        internal bool RegisterValidator( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m ) => RegisterValidatorOrPostHandler( monitor, impl, m, true );

        internal bool RegisterPostHandler( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m ) => RegisterValidatorOrPostHandler( monitor, impl, m, false );

        bool RegisterValidatorOrPostHandler( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m, bool validator )
        {
            (ParameterInfo[]? parameters, ParameterInfo? p, IReadOnlyList<IPocoRootInfo>? commands) = GetImpactedCommands( monitor, impl, m );
            if( p != null )
            {
                Debug.Assert( parameters != null );
                bool success = true;
                Debug.Assert( commands != null, "p == null <==> commands == null" );
                foreach( var command in commands )
                {
                    Debug.Assert( IndexedCommands.ContainsKey( command ), "Since parameters are filtered by registered Poco." );
                    var e = IndexedCommands[command];
                    success &= validator
                                ? e.AddValidator( monitor, impl, m, parameters, p )
                                : e.AddPostHandler( monitor, impl, m, parameters, p );
                }
                return success;
            }
            return false;
        }

        (ParameterInfo[]? Parameters, ParameterInfo? Param, IReadOnlyList<IPocoRootInfo>? Commands) GetImpactedCommands( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m )
        {
            (ParameterInfo[]? parameters, ParameterInfo? p, IPocoInterfaceInfo? commandInterface) = GetCommandCandidates( monitor, m );
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
                var (index,commands) = CreateCommandMap( monitor, pocoResult );
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


        CommandRegistry( IReadOnlyDictionary<object, Entry> index, IReadOnlyList<Entry> commands, IPocoSupportResult poco )
        {
            IndexedCommands = index;
            Commands = commands;
            _pocoResult = poco;
        }

        static (Dictionary<object, Entry>?,IReadOnlyList<Entry>?) CreateCommandMap( IActivityMonitor monitor, IPocoSupportResult pocoResult )
        {
            bool success = true;
            var index = new Dictionary<object, Entry>();
            var commands = new List<Entry>();
            if( pocoResult.OtherInterfaces.TryGetValue( typeof( ICommand ), out IReadOnlyList<IPocoRootInfo>? commandPocos ) )
            {
                foreach( var poco in commandPocos )
                {
                    Debug.Assert( poco.IsClosedPoco && typeof( ICommand ).IsAssignableFrom( poco.ClosureInterface ) );
                    var entry = Entry.Create( monitor, poco, commands.Count );
                    if( entry == null ) success = false;
                    else
                    {
                        commands.Add( entry );
                        foreach( var name in entry.PreviousNames.Append( entry.CommandName ) )
                        {
                            if( index.TryGetValue( name, out var exists ) )
                            {
                                monitor.Error( $"The command name '{name}' clashes: both '{entry.Command.PrimaryInterface.AssemblyQualifiedName}' and '{exists.Command.PrimaryInterface.AssemblyQualifiedName}' share it." );
                                success = false;
                            }
                            else index.Add( name, entry );
                        }
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

        (ParameterInfo[]? Parameters, ParameterInfo? Param, IPocoInterfaceInfo? CommandInterface) GetCommandCandidates( IActivityMonitor monitor, MethodInfo m )
        {
            var parameters = m.GetParameters();
            var candidates = parameters.Select( p => (p, _pocoResult.AllInterfaces.GetValueOrDefault( p.ParameterType )) )
                                                                    .Where( x => x.Item2 != null
                                                                                && x.Item2.Root.IsClosedPoco
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
