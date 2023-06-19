using CK.Core;
using CK.Cris;
using Microsoft.Extensions.DependencyInjection;
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
    /// Holds the set of <see cref="Entry"/> that have been discovered and index them by
    /// <see cref="IPocoRootInfo"/>.
    /// </summary>
    public partial class CrisRegistry
    {
        readonly IReadOnlyDictionary<IPocoRootInfo, Entry> _indexedEntries;

        /// <summary>
        /// Gets the root information on all available Poco.
        /// </summary>
        public IPocoSupportResult PocoResult { get; }

        /// <summary>
        /// Exposes the ambient values poco.
        /// </summary>
        public IPocoRootInfo AmbientValues { get; }

        /// <summary>
        /// Gets all the discovered commands ordered by their <see cref="Entry.CrisPocoIndex"/>.
        /// </summary>
        public IReadOnlyList<Entry> CrisPocoModels { get; }

        /// <summary>
        /// Finds a command or event entry from its Poco definition.
        /// </summary>
        /// <param name="poco">The poco definition.</param>
        /// <returns>The entry or null.</returns>
        public Entry? Find( IPocoRootInfo poco ) => _indexedEntries.GetValueOrDefault( poco );

        internal bool RegisterHandler( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m, bool allowUnclosed, string? fileName, int lineNumber )
        {
            (Entry? e, ParameterInfo[]? parameters, ParameterInfo? p) = GetSingleCommandEntry( monitor, "Handler", m );
            if( e == null ) return false;
            Debug.Assert( parameters != null && p != null );
            bool isClosedHandler = p.ParameterType == e.CrisPocoInfo.ClosureInterface;
            if( !isClosedHandler && !allowUnclosed )
            {
                allowUnclosed = p.GetCustomAttributes().Any( a => a.GetType().FindInterfaces( (i,n) => i.Name == (string?)n, nameof(IAllowUnclosedCommandAttribute) ).Length > 0 );
                if( !allowUnclosed )
                {
                    monitor.Info( $"Method {MethodName( m, parameters )} cannot handle '{e.PocoName}' command because type {p.ParameterType.Name} doesn't represent the whole command." );
                    return true;
                }
            }
            return e.AddHandler( monitor, impl, m, parameters, p, isClosedHandler, fileName, lineNumber );
        }

        internal bool RegisterPostHandler( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m, string? fileName, int lineNumber )
        {
            (Entry? e, ParameterInfo[]? parameters, ParameterInfo? p) = GetSingleCommandEntry( monitor, "PostHandler", m );
            if( e == null ) return false;
            Debug.Assert( parameters != null && p != null );
            return e.AddPostHandler( monitor, impl, m, parameters, p, fileName, lineNumber );
        }

        internal bool RegisterValidator( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m, string? fileName, int lineNumber )
        {
            return RegisterValidatorOrRoutedEventHandler( monitor, impl, m, fileName, lineNumber, isValidator: true );
        }

        internal bool RegisterRoutedEventHandler( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m, string? fileName, int lineNumber )
        {
            return RegisterValidatorOrRoutedEventHandler( monitor, impl, m, fileName, lineNumber, isValidator: false );
        }

        bool RegisterValidatorOrRoutedEventHandler( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m, string? fileName, int lineNumber, bool isValidator )
        {
            (ParameterInfo[]? parameters, ParameterInfo? p, IReadOnlyList<IPocoRootInfo>? families) = GetImpactedFamilies( monitor, impl, m, expectCommands: isValidator );
            if( p != null )
            {
                Debug.Assert( parameters != null );
                bool success = true;
                Debug.Assert( families != null, "p == null <==> families == null" );
                if( families.Count == 0 )
                {
                    monitor.Info( $"Method {MethodName( m, parameters )} is unused since no {(isValidator ? "command" : "event")} match the '{p.Name}' parameter." );
                }
                else
                {
                    var execContext = parameters.FirstOrDefault( p => p.ParameterType == typeof( ICrisCommandContext ) );
                    if( execContext != null )
                    {
                        monitor.Error( $"Invalid parameter '{execContext.Name}' in method '{MethodName( m, parameters )}': ICrisExecutionContext cannot " +
                                       $"be used in a {(isValidator ? "command validator" : "routed event handler")} since they cannot execute commands or send events." );
                        success = false;
                    }
                    if( isValidator )
                    {
                        var callContext = parameters.FirstOrDefault( p => p.ParameterType == typeof( ICrisEventContext ) );
                        if( callContext != null )
                        {
                            monitor.Error( $"Invalid parameter '{callContext.Name}' in method '{MethodName( m, parameters )}': ICrisCallContext cannot " +
                                           $"be used in a command validator since validators cannot execute commands." );
                            success = false;
                        }
                    }
                    foreach( var family in families )
                    {
                        Debug.Assert( _indexedEntries.ContainsKey( family ), "Since parameters are filtered by registered Poco." );
                        var e = _indexedEntries[family];
                        success &= isValidator
                                    ? e.AddValidator( monitor, impl, m, parameters, p, fileName, lineNumber )
                                    : e.AddRoutedEventHandler( monitor, impl, m, parameters, p, fileName, lineNumber );
                    }
                }
                return success;
            }
            return false;
        }

        (ParameterInfo[]? Parameters, ParameterInfo? Param, IReadOnlyList<IPocoRootInfo>? Families) GetImpactedFamilies( IActivityMonitor monitor,
                                                                                                                         IStObjFinalClass impl,
                                                                                                                         MethodInfo m,
                                                                                                                         bool expectCommands )
        {
            (ParameterInfo[]? parameters, ParameterInfo? p, IPocoInterfaceInfo? pocoInterface) = GetSingleOrDefaultConcreteCandidate( monitor, m, expectCommands );
            if( parameters == null ) return (null, null, null);
            if( p != null )
            {
                Debug.Assert( pocoInterface != null, "Since we have a concrete IAbstractCommand or IEvent parameter." );
                return (parameters, p, new IPocoRootInfo[] { pocoInterface.Root });
            }
            // Looking for parts.
            var (param, families) = GetFamiliesFromPart( monitor, m, parameters, expectCommands );
            if( param != null )
            {
                return (parameters, param, families);
            }
            return (parameters, null, null);
        }

        (ParameterInfo? Parameter, IReadOnlyList<IPocoRootInfo>? Families) GetFamiliesFromPart( IActivityMonitor monitor,
                                                                                                MethodInfo m,
                                                                                                ParameterInfo[] parameters,
                                                                                                bool expectCommands )
        {
            var tPart = expectCommands ? typeof( ICommandPart ) : typeof( IEventPart );
            var candidates = parameters.Where( param => tPart.IsAssignableFrom( param.ParameterType ) )
                                        .Select( param => (param, partsList: PocoResult.OtherInterfaces.GetValueOrDefault( param.ParameterType, Array.Empty<IPocoRootInfo>() )! ) )
                                        .ToArray();
            if( candidates.Length == 1 )
            {
                Debug.Assert( candidates[0].partsList != null, "Nulls have been filtered out." );
                return (candidates[0].param, candidates[0].partsList);
            }
            var expected = expectCommands ? "ICommand, ICommand<TResult> or ICommandPart" : "IEvent or IEventPart";
            if( candidates.Length > 1 )
            {
                monitor.Error( $"Method {MethodName( m, parameters )} cannot have more than one {expected} parameter." );
            }
            else if( candidates.Length == 0 )
            {
                monitor.Error( $"Method {MethodName( m, parameters )}: missing a {expected} parameter." );
            }
            return (null, null);
        }

        /// <summary>
        /// Gets a <see cref="CrisRegistry"/> for a <see cref="ICodeGenerationContext"/> (it has been created
        /// by <see cref="FindOrCreate"/> during the CSharp code generation phase).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="c">The code generation context.</param>
        /// <returns>The registry or null if it has been created yet.</returns>
        public static CrisRegistry? Find( IActivityMonitor monitor, ICodeGenerationContext c )
        {
            c.CurrentRun.Memory.TryGetCachedInstance<CrisRegistry>( out var r );
            return r;
        }

        /// <summary>
        /// Gets or builds a <see cref="CrisRegistry"/> for the current run of a <see cref="ICodeGenerationContext"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="c">The generated path.</param>
        /// <returns>The directory or null on error.</returns>
        public static CrisRegistry? FindOrCreate( IActivityMonitor monitor, ICodeGenerationContext c )
        {
            if( !c.CurrentRun.Memory.TryGetCachedInstance<CrisRegistry>( out var result ) )
            {
                var pocoResult = c.CurrentRun.ServiceContainer.GetRequiredService<IPocoSupportResult>();
                var (index,entries) = CreateAllEntries( monitor, c.CurrentRun.EngineMap, pocoResult );
                if( entries != null )
                {
                    Debug.Assert( index != null, "Since entries is not null." );
                    monitor.Info( $"{entries.Count} commands or events detected." );
                    var av = pocoResult.Find( typeof( CK.Cris.AmbientValues.IAmbientValues ) );
                    if( av == null )
                    {
                        monitor.Error( "CK.Cris.AmbientValues.IAmbientValues must be registered." );
                    }
                    else
                    {
                        result = new CrisRegistry( index, entries, pocoResult, av.Root );
                    }
                }
                c.CurrentRun.Memory.AddCachedInstance( result );
            }
            return result;
        }


        CrisRegistry( IReadOnlyDictionary<IPocoRootInfo, Entry> index, IReadOnlyList<Entry> entries, IPocoSupportResult poco, IPocoRootInfo ambientValues )
        {
            _indexedEntries = index;
            CrisPocoModels = entries;
            PocoResult = poco;
            AmbientValues = ambientValues;
        }

        static (Dictionary<IPocoRootInfo, Entry>?,IReadOnlyList<Entry>?) CreateAllEntries( IActivityMonitor monitor, IStObjEngineMap services, IPocoSupportResult pocoResult )
        {
            bool success = true;
            var index = new Dictionary<IPocoRootInfo, Entry>();
            var entries = new List<Entry>();
            // Kindly handle the fact that no ICrisPoco exist. This should not happen since IAmbientValues at least should be registered.
            if( pocoResult.OtherInterfaces.TryGetValue( typeof( IAbstractCommand ), out IReadOnlyList<IPocoRootInfo>? commands ) )
            {
                foreach( var poco in commands )
                {
                    var hServices = poco.Interfaces.Select( i => typeof( ICommandHandler<> ).MakeGenericType( i.PocoInterface ) )
                                                   .Select( gI => (itf: gI, impl: services.ToLeaf( gI )) )
                                                   .Where( m => m.impl != null )
                                                   .GroupBy( m => m.impl )
                                                   .ToArray();
                    if( hServices.Length > 1 )
                    {
                        monitor.Error( $"Ambiguous command handler '{hServices.Select( m => $"{m.Key!.ClassType:C}' implements '{m.Select( x => x.itf.ToCSharpName() ).Concatenate( "' ,'" )}" )}': only one service can eventually handle a command." );
                        success = false;
                    }
                    var entry = Entry.Create( monitor, pocoResult, poco, entries.Count, services );
                    if( entry == null ) success = false;
                    else
                    {
                        entries.Add( entry );
                        index.Add( entry.CrisPocoInfo, entry );
                    }
                }
            }
            if( pocoResult.OtherInterfaces.TryGetValue( typeof( IEvent ), out IReadOnlyList<IPocoRootInfo>? events ) )
            {
                foreach( var poco in events )
                {
                    var entry = Entry.Create( monitor, pocoResult, poco, entries.Count, services );
                    if( entry == null ) success = false;
                    else
                    {
                        entries.Add( entry );
                        index.Add( entry.CrisPocoInfo, entry );
                    }
                }
            }
            return success ? (index, entries) : (null, null);
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


        (Entry? e, ParameterInfo[]? parameters, ParameterInfo? p) GetSingleCommandEntry( IActivityMonitor monitor, string kind, MethodInfo m )
        {
            (ParameterInfo[]? parameters, ParameterInfo? p, IPocoInterfaceInfo? commandInterface) = GetSingleOrDefaultConcreteCandidate( monitor, m, expectCommand: true );
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
                    Debug.Assert( _indexedEntries.ContainsKey( commandInterface.Root ), "Since parameters are filtered by registered Poco." );
                    return (_indexedEntries[commandInterface.Root], parameters, p);
                }
            }
            return (null, null, null);
        }

        (ParameterInfo[]? Parameters, ParameterInfo? Param, IPocoInterfaceInfo? ParamInterface) GetSingleOrDefaultConcreteCandidate( IActivityMonitor monitor,
                                                                                                                                     MethodInfo m,
                                                                                                                                     bool expectCommand )
        {
            var parameters = m.GetParameters();
            var candidates = parameters.Select( p => (p, PocoResult.AllInterfaces.GetValueOrDefault( p.ParameterType )) )
                                                                    .Where( x => x.Item2 != null
                                                                                && typeof( ICrisPoco ).IsAssignableFrom( x.Item2.PocoInterface ) )
                                                                    .ToArray();
            if( candidates.Length > 1 )
            {
                string tooMuch = candidates.Select( c => $"{c.p.ParameterType.ToCSharpName()} {c.p.Name}" ).Concatenate();
                monitor.Error( $"Method {MethodName( m, parameters )} cannot have more than one concrete command or event parameter. Found: {tooMuch}." );
                return (null, null, null);
            }
            if( candidates.Length == 0 )
            {
                return (parameters, null, null);
            }
            var paramInfo = candidates[0].p;
            Debug.Assert( candidates[0].Item2 != null && paramInfo.ParameterType == candidates[0].Item2!.PocoInterface );
            bool isCommand = typeof( IAbstractCommand ).IsAssignableFrom( paramInfo.ParameterType );            
            if( isCommand != expectCommand )
            {
                if( expectCommand )
                {
                    monitor.Error( $"Method {MethodName( m, parameters )} must have a ICommand or ICommand<TResult> parameter. Parameter '{paramInfo.Name}' is a IEvent." );
                }
                else
                {
                    monitor.Error( $"Method {MethodName( m, parameters )} must have a IEvent parameter. Parameter '{paramInfo.Name}' is a ICommand or ICommand<TResult>." );
                }
                return (null, null, null);
            }
            return (parameters, paramInfo, candidates[0].Item2);
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
