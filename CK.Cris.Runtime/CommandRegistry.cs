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
        /// <summary>
        /// Gets the discovered commands indexed by their <see cref="Entry.CommandName"/>, <see cref="Entry.PreviousNames"/> and
        /// the <see cref="IPocoRootInfo"/>.
        /// </summary>
        public IReadOnlyDictionary<object, Entry> IndexedCommands { get; }

        /// <summary>
        /// Gets all the discovered commands.
        /// </summary>
        public IReadOnlyList<Entry> Commands { get; }

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
                IStObjServiceEngineMap services = c.CurrentRun.EngineMap.Services;
                IPocoSupportResult pocoResult = c.Assembly.GetPocoSupportResult();

                var (index,commands) = CreateCommandMap( monitor, pocoResult );
                if( commands != null )
                {
                    Debug.Assert( index != null, "Since commands is not null." );
                    bool success = true;
                    if( commands.Count > 0 )
                    {
                        Debug.Assert( typeof( CommandDirectory ).Namespace == "CK.Cris" );

                        using( monitor.OpenInfo( $"Registering {commands.Count} commands." ) )
                        {
                            foreach( IStObjFinalClass impl in services.GetAllMappings() )
                            {
                                if( impl.ClassType.Namespace == "CK.Cris" ) continue;
                                foreach( var m in impl.ClassType.GetMethods() )
                                {
                                    bool handleAsync = false;
                                    bool validateAsync = false;
                                    if( m.Name == "HandleCommand" || (handleAsync = (m.Name == "HandleCommandAsync")) )
                                    {
                                        (ParameterInfo[]? parameters, ParameterInfo? p, IPocoInterfaceInfo? commandInterface) = GetCommonCandidates( monitor, pocoResult, m );
                                        if( parameters == null )
                                        {
                                            success = false;
                                        }
                                        else
                                        {
                                            if( p == null )
                                            {
                                                monitor.Debug( $"Method {MethodName( m, parameters )} skipped. No ICommand parameter detected." );
                                            }
                                            else
                                            {
                                                Debug.Assert( commandInterface != null, "Since we have a parameter." );
                                                Debug.Assert( commandInterface.Root.ClosureInterface != null, "Since this a ICommand : IClosedPoco." );
                                                Debug.Assert( index.ContainsKey( commandInterface.Root ), "Since parameters are filtered by registered Poco." );
                                                Entry cmd = index[commandInterface.Root];
                                                if( commandInterface.PocoInterface != commandInterface.Root.ClosureInterface )
                                                {
                                                    cmd.AddUnclosedHandler( monitor, m, parameters, p, commandInterface );
                                                }
                                                else
                                                {
                                                    success &= cmd.AddHandler( monitor, impl, m, handleAsync, parameters, p );
                                                }
                                            }
                                        }
                                    }
                                    else if( m.Name == "ValidateCommand" || (validateAsync = (m.Name == "ValidateCommandAsync")) )
                                    {
                                        (ParameterInfo[]? parameters, ParameterInfo? p, IPocoInterfaceInfo? commandInterface) = GetCommonCandidates( monitor, pocoResult, m );
                                        if( parameters == null )
                                        {
                                            success = false;
                                        }
                                        else
                                        {
                                            IReadOnlyList<IPocoRootInfo>? toValidate = null;
                                            if( p != null )
                                            {
                                                Debug.Assert( commandInterface != null, "Since we have a ICommand parameter." );
                                                toValidate = new IPocoRootInfo[] { commandInterface.Root };
                                            }
                                            else
                                            {
                                                // Looking for command parts.
                                                var candidates = parameters.Where( param => typeof( ICommandPart ).IsAssignableFrom( param.ParameterType ) )
                                                                            .Select( param => (param, commandPartsList: pocoResult.OtherInterfaces.GetValueOrDefault( param.ParameterType )) )
                                                                            .Where( x => x.Item2 != null )
                                                                            .ToArray();
                                                if( candidates.Length > 1 )
                                                {
                                                    monitor.Error( $"Method {MethodName( m, parameters )} cannot have more than one ICommand or ICommandPart parameter." );
                                                    success = false;
                                                }
                                                else if( candidates.Length == 0 )
                                                {
                                                    monitor.Debug( $"Method {MethodName( m, parameters )} skipped. No ICommand nor ICommandPart parameter detected." );
                                                }
                                                else
                                                {
                                                    Debug.Assert( candidates[0].commandPartsList != null, "Nulls have been filtered out." );
                                                    p = candidates[0].param;
                                                    toValidate = candidates[0].commandPartsList;
                                                }
                                            }
                                            if( p != null )
                                            {
                                                Debug.Assert( toValidate != null, "p == null <==> commands == null" );
                                                foreach( var command in toValidate )
                                                {
                                                    Debug.Assert( index.ContainsKey( command ), "Since parameters are filtered by registered Poco." );
                                                    success &= index[command].AddValidator( monitor, impl, m, validateAsync, parameters, p );
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if( !success )
                            {
                                monitor.CloseGroup( "Failed to create CommandRegistrar." );
                            }
                            else
                            {
                                result = new CommandRegistry( index, commands );
                            }
                        }
                    }
                    else
                    {
                        monitor.Info( "No Command detected." );
                        result = new CommandRegistry( ImmutableDictionary<object, Entry>.Empty, Array.Empty<Entry>() );
                    }
                }
                c.Assembly.Memory.AddCachedInstance( result );
            }
            return result;
        }


        CommandRegistry( IReadOnlyDictionary<object, Entry> index, IReadOnlyList<Entry> commands )
        {
            IndexedCommands = index;
            Commands = commands;
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

        static (ParameterInfo[]? Parameters, ParameterInfo? Param, IPocoInterfaceInfo? CommandInterface) GetCommonCandidates( IActivityMonitor monitor, IPocoSupportResult poco, MethodInfo m )
        {
            var parameters = m.GetParameters();
            var candidates = parameters.Select( p => (p, poco.AllInterfaces.GetValueOrDefault( p.ParameterType )) )
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

        static void CheckSyncAsyncMethodName( IActivityMonitor monitor, MethodInfo method, ParameterInfo[] parameters, bool isAsyncName, bool isAsyncRet )
        {
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
