using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Setup.Cris
{

    /// <summary>
    /// Internal registry. Instantiated by the <see cref="CrisDirectoryImpl"/> once IPocoTypeSystem is available.
    /// Attributes (<see cref="HandlerBase"/>) wait for it to register their handlers.
    /// Once done, the public ICrisDirectoryServiceEngine is published so that Cris dependent components can use it.
    /// </summary>
    internal sealed class CrisTypeRegistry : ICrisDirectoryServiceEngine
    {
        readonly IReadOnlyDictionary<IPrimaryPocoType, CrisType> _indexedEntries;
        readonly IReadOnlyList<CrisType> _entries;
        readonly IPocoTypeSystem _typeSystem;
        readonly IAbstractPocoType? _crisPocoType;
        readonly IAbstractPocoType? _crisCommandType;
        readonly IAbstractPocoType? _crisCommandTypePart;
        readonly IAbstractPocoType? _crisEventType;
        readonly IAbstractPocoType? _crisEventTypePart;

        CrisTypeRegistry( IReadOnlyDictionary<IPrimaryPocoType,CrisType> index,
                          IReadOnlyList<CrisType> entries,
                          IPocoTypeSystem typeSystem,
                          IAbstractPocoType? crisPocoType,
                          IAbstractPocoType? crisCommandType,
                          IAbstractPocoType? crisCommandTypePart,
                          IAbstractPocoType? crisEventType,
                          IAbstractPocoType? crisEventTypePart )
        {
            _indexedEntries = index;
            _entries = entries;
            _typeSystem = typeSystem;
            _crisPocoType = crisPocoType;
            _crisCommandType = crisCommandType;
            _crisCommandTypePart = crisCommandTypePart;
            _crisEventType = crisEventType;
            _crisEventTypePart = crisEventTypePart;
        }

        /// <inheritdoc/>
        public IPocoTypeSystem TypeSystem => _typeSystem;

        /// <inheritdoc/>
        public IReadOnlyList<CrisType> CrisTypes => _entries;

        /// <inheritdoc/>
        public CrisType? Find( IPrimaryPocoType poco ) => _indexedEntries.GetValueOrDefault( poco );

        internal static CrisTypeRegistry? Create( IActivityMonitor monitor, IPocoTypeSystem typeSystem, IStObjEngineMap services )
        {
            bool success = true;
            var entries = new List<CrisType>();
            var indexedEntries = new Dictionary<IPrimaryPocoType, CrisType>();

            IPocoGenericTypeDefinition? commandWithResultType = typeSystem.FindGenericTypeDefinition( typeof( ICommand<> ) );

            IAbstractPocoType? crisCommandTypePart = GetAbstractPocoType( monitor, typeSystem, typeof( ICommandPart ) );
            IAbstractPocoType? crisCommandType = crisCommandTypePart?.MinimalGeneralizations.Single()
                                                    ?? GetAbstractPocoType( monitor, typeSystem, typeof( IAbstractCommand ) );

            IAbstractPocoType? crisEventTypePart = GetAbstractPocoType( monitor, typeSystem, typeof( IEventPart ) );
            IAbstractPocoType? crisEventType = crisEventTypePart?.MinimalGeneralizations.Single()
                                                    ?? GetAbstractPocoType( monitor, typeSystem, typeof( IEvent ) );
            IAbstractPocoType? crisPocoType = null;

            if( crisCommandType != null )
            {
                crisPocoType = crisCommandType.Generalizations.Single();
                foreach( var command in crisCommandType.PrimaryPocoTypes )
                {
                    CrisType? entry = CreateCommandEntry( monitor, commandWithResultType, crisEventType, services, entries.Count, command );
                    if( entry == null ) success = false;
                    else
                    {
                        entries.Add( entry );
                        indexedEntries.Add( entry.CrisPocoType, entry );
                    }
                }
                monitor.Trace( $"Found {entries.Count} Cris commands." );
            }
            // Don't handle events of command handling failed.
            if( !success ) return null;

            if( crisEventType != null )
            {
                crisPocoType ??= crisEventType.Generalizations.Single();
                if( crisEventTypePart != null )
                {
                    foreach( var evPart in crisEventTypePart.AllSpecializations )
                    {
                        success &= CheckNoRoutedOrImmediateAttribute( monitor, evPart.Type );
                    }
                }
                int commandCount = entries.Count;
                foreach( var ev in crisEventType.PrimaryPocoTypes )
                {
                    CrisType? entry = CreateEventEntry( monitor, crisCommandType, entries.Count, ev );
                    if( entry == null ) success = false;
                    else
                    {
                        entries.Add( entry );
                        indexedEntries.Add( entry.CrisPocoType, entry );
                    }
                }
                monitor.Trace( $"Found {entries.Count - commandCount} Cris commands." );
            }
            return success
                    ? new CrisTypeRegistry( indexedEntries, entries, typeSystem, crisPocoType, crisCommandType, crisCommandTypePart, crisEventType, crisEventTypePart )
                    : null;

            static IAbstractPocoType? GetAbstractPocoType( IActivityMonitor monitor, IPocoTypeSystem typeSystem, Type type )
            {
                var p = typeSystem.FindByType<IAbstractPocoType>( type );
                if( p == null || p.ImplementationLess )
                {
                    monitor.Info( $"No Cris {type.Name} discovered." );
                    p = null;
                }
                else
                {
                    Throw.DebugAssert( p.IsNullable );
                    p = p.NonNullable;
                }
                return p;
            }

            static CrisType? CreateCommandEntry( IActivityMonitor monitor,
                                                 IPocoGenericTypeDefinition? commandWithResultType,
                                                 IAbstractPocoType? crisEventType,
                                                 IStObjEngineMap services,
                                                 int crisPocoIndex,
                                                 IPrimaryPocoType command )
            {
                if( crisEventType != null && command.AbstractTypes.Contains( crisEventType ) )
                {
                    monitor.Error( $"Cris '{command}' cannot be both a IEvent and a IAbstractCommand." );
                    return null;
                }
                bool success = true;
                CrisPocoKind kind = CrisPocoKind.Command;
                IPocoType? resultType = null;
                if( commandWithResultType != null )
                {
                    var withResult = command.AbstractTypes.Where( a => a.GenericTypeDefinition == commandWithResultType );
                    var reduced = withResult.ComputeMinimal();
                    if( reduced.Count > 1 )
                    {
                        monitor.Error( $"Command '{command}' declares incompatible results '{reduced.Select( a => a.ToString() ).Concatenate( "' ,'" )}': " +
                                       $"result types are incompatible and cannot be reduced." );
                        success = false;
                    }
                    else if( reduced.Count == 1 )
                    {
                        resultType = reduced[0].GenericArguments[0].Type;
                        kind = CrisPocoKind.CommandWithResult;
                    }
                }
                var handlerServices = command.SecondaryTypes.Select( s => s.Type ).Append( command.Type )
                                                .Select( i => typeof( ICommandHandler<> ).MakeGenericType( i ) )
                                                .Select( gI => (itf: gI, impl: services.ToLeaf( gI )) )
                                                .Where( m => m.impl != null )
                                                .GroupBy( m => m.impl );
                IStObjFinalClass? commandHandlerService = handlerServices.FirstOrDefault()?.Key;
                if( commandHandlerService != null && handlerServices.Count() > 1 )
                {
                    monitor.Error( $"Ambiguous command handler '{handlerServices.Select( m => $"{m.Key!.ClassType:C}' implements '{m.Select( x => x.itf.ToCSharpName() ).Concatenate( "' ,'" )}" )}': only one service can eventually handle a command." );
                    success = false;
                }
                return success ? new CrisType( command, crisPocoIndex, resultType, commandHandlerService, kind ) : null;
            }

            static CrisType? CreateEventEntry( IActivityMonitor monitor,
                                               IAbstractPocoType? crisCommandType,
                                               int crisPocoIndex,
                                               IPrimaryPocoType ev )
            {
                Throw.DebugAssert( "Check is already done by CreateCommandEntry.",
                                   crisCommandType == null || !ev.AbstractTypes.Contains( crisCommandType ) );
                Throw.DebugAssert( !ev.IsNullable );
                if( !GetKindFromAttributesAndCheckOtherInterfaces( monitor, ev, out var kind ) )
                {
                    return null;
                }
                return new CrisType( ev, crisPocoIndex, null, null, kind );

                static bool GetKindFromAttributesAndCheckOtherInterfaces( IActivityMonitor monitor, IPrimaryPocoType ev, out CrisPocoKind kind )
                {
                    bool isRouted = ev.Type.GetCustomAttribute<RoutedEventAttribute>() != null;
                    bool isImmediate = ev.Type.GetCustomAttribute<ImmediateEventAttribute>() != null;
                    kind = isRouted
                            ? (isImmediate ? CrisPocoKind.RoutedImmediateEvent : CrisPocoKind.RoutedEvent)
                            : (isImmediate ? CrisPocoKind.CallerOnlyImmediateEvent : CrisPocoKind.CallerOnlyEvent);
                    bool success = true;
                    foreach( var i in ev.SecondaryTypes )
                    {
                        success &= CheckNoRoutedOrImmediateAttribute( monitor, i.Type );
                    }
                    return success;
                }
            }
        }

        private static bool CheckNoRoutedOrImmediateAttribute( IActivityMonitor monitor, Type? i )
        {
            bool success = true;
            var attrs = i.GetCustomAttributesData();
            foreach( var a in attrs )
            {
                if( a.AttributeType == typeof( RoutedEventAttribute ) )
                {
                    monitor.Error( $"Interface '{i:C}' cannot be decorated with [RoutedEvent]. Only a primary IEvent interface can use this attribute." );
                    success = false;
                }
                if( a.AttributeType == typeof( ImmediateEventAttribute ) )
                {
                    monitor.Error( $"Interface '{i:C}' cannot be decorated with [ImmediateEvent]. Only a primary IEvent interface can use this attribute." );
                    success = false;
                }
            }
            return success;
        }

        internal bool RegisterHandler( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m, bool allowUnclosed, string? fileName, int lineNumber )
        {
            (CrisType? e, ParameterInfo[]? parameters, ParameterInfo? p) = GetSingleCommandEntry( monitor, "Handler", m );
            if( e == null ) return false;
            Throw.DebugAssert( parameters != null && p != null );
            bool isClosedHandler = p.ParameterType == e.CrisPocoType.FamilyInfo.ClosureInterface;
            if( !isClosedHandler && !allowUnclosed )
            {
                allowUnclosed = p.GetCustomAttributes().Any( a => a.GetType().FindInterfaces( ( i, n ) => i.Name == (string?)n, nameof( IAllowUnclosedCommandAttribute ) ).Length > 0 );
                if( !allowUnclosed )
                {
                    monitor.Info( $"Method {CrisType.MethodName( m, parameters )} cannot handle '{e.PocoName}' command because type '{p.ParameterType.Name}' doesn't represent the whole command." );
                    return true;
                }
            }
            return e.AddHandler( monitor, impl, m, parameters, p, isClosedHandler, fileName, lineNumber );
        }

        internal bool RegisterPostHandler( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m, string? fileName, int lineNumber )
        {
            (CrisType? e, ParameterInfo[]? parameters, ParameterInfo? p) = GetSingleCommandEntry( monitor, "PostHandler", m );
            if( e == null ) return false;
            Throw.DebugAssert( parameters != null && p != null );
            return e.AddPostHandler( monitor, _typeSystem, impl, m, parameters, p, fileName, lineNumber );
        }

        (CrisType? e, ParameterInfo[]? parameters, ParameterInfo? p) GetSingleCommandEntry( IActivityMonitor monitor, string kind, MethodInfo m )
        {
            (ParameterInfo[]? parameters, ParameterInfo? p, IPrimaryPocoType? candidate) = TryGetCrisTypeCandidate( monitor, m, expectCommand: true );
            if( parameters != null )
            {
                if( p == null )
                {
                    monitor.Error( $"Invalid {kind} method {CrisType.MethodName( m, parameters )} skipped. No IAbstractCommand parameter detected." );
                }
                else
                {
                    Throw.DebugAssert( "Since we have a parameter.", candidate != null );
                    Throw.DebugAssert( parameters != null );
                    Throw.DebugAssert( "Since parameters are filtered by registered Poco.", _indexedEntries.ContainsKey( candidate ) );
                    return (_indexedEntries[candidate], parameters, p);
                }
            }
            return (null, null, null);
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
            (ParameterInfo[]? parameters, ParameterInfo? p, IReadOnlyList<IPrimaryPocoType>? candidates) = GetCandidates( monitor, m, expectCommands: isValidator );
            if( p != null )
            {
                Throw.DebugAssert( parameters != null );
                bool success = true;
                Throw.DebugAssert( "p == null <==> candidates == null", candidates != null );
                if( candidates.Count == 0 )
                {
                    monitor.Info( $"Method {CrisType.MethodName( m, parameters )} is unused since no {(isValidator ? "command" : "event")} match the '{p.Name}' parameter." );
                }
                else
                {
                    var execContext = parameters.FirstOrDefault( p => p.ParameterType == typeof( ICrisCommandContext ) );
                    if( execContext != null )
                    {
                        monitor.Error( $"Invalid parameter '{execContext.Name}' in method '{CrisType.MethodName( m, parameters )}': {nameof(ICrisCommandContext)} cannot " +
                                       $"be used in a {(isValidator ? "command validator" : "routed event handler")} since they cannot execute commands or send events." );
                        success = false;
                    }
                    ParameterInfo? validatorUserMessageCollector = null;
                    if( isValidator )
                    {
                        var callContext = parameters.FirstOrDefault( p => p.ParameterType == typeof( ICrisEventContext ) );
                        if( callContext != null )
                        {
                            monitor.Error( $"Invalid parameter '{callContext.Name}' in method '{CrisType.MethodName( m, parameters )}': {nameof(ICrisEventContext)} cannot " +
                                           $"be used in a command validator since validators cannot execute commands." );
                            success = false;
                        }
                        validatorUserMessageCollector = parameters.FirstOrDefault( p => p.ParameterType == typeof( UserMessageCollector ) );
                        if( validatorUserMessageCollector == null )
                        {
                            monitor.Error( $"Command validator method '{CrisType.MethodName( m, parameters )}' must take a 'UserMessageCollector' parameter " +
                                           $"to collect validation errors, warnings and informations." );
                            success = false;
                        }
                    }
                    if( success )
                    {
                        foreach( var family in candidates )
                        {
                            Throw.DebugAssert( _indexedEntries.ContainsKey( family ), "Since parameters are filtered by registered Poco." );
                            var e = _indexedEntries[family];
                            success &= isValidator
                                        ? e.AddValidator( monitor, impl, m, parameters, p, validatorUserMessageCollector!, fileName, lineNumber )
                                        : e.AddRoutedEventHandler( monitor, impl, m, parameters, p, fileName, lineNumber );
                        }
                    }
                }
                return success;
            }
            return false;
        }

        (IPrimaryPocoType? P, bool IsCommand) GetPrimary( Type t ) => GetPrimary( _typeSystem.FindByType( t ) );

        (IPrimaryPocoType? P, bool IsCommand) GetPrimary( IPocoType? tPoco )
        {
            if( tPoco is not IPrimaryPocoType p )
            {
                if( tPoco is not ISecondaryPocoType s ) return (null, false);
                p = s.PrimaryPocoType;
            }
            Throw.DebugAssert( p.IsNullable );
            p = p.NonNullable;
            foreach( var a in p.AbstractTypes )
            {
                if( a == _crisCommandType ) return (p, true);
                if( a == _crisEventType )
                {
                    return (p, false);
                }
            }
            return (null, false);
        }

        (ParameterInfo[]? Parameters, ParameterInfo? Param, IPrimaryPocoType? Candidate) TryGetCrisTypeCandidate( IActivityMonitor monitor,
                                                                                                                  MethodInfo m,
                                                                                                                  bool expectCommand )
        {
            var parameters = m.GetParameters();
            var candidates = parameters.Select( p => (p, GetPrimary( p.ParameterType )) )
                                       .Where( x => x.Item2.P != null )
                                       .ToArray();
            if( candidates.Length > 1 )
            {
                string tooMuch = candidates.Select( c => $"{c.p.ParameterType.ToCSharpName()} {c.p.Name}" ).Concatenate();
                monitor.Error( $"Method {CrisType.MethodName( m, parameters )} cannot have more than one concrete {(expectCommand ? "command" : "event")} parameter. Found: {tooMuch}" );
                return (null, null, null);
            }
            if( candidates.Length == 0 )
            {
                return (parameters, null, null);
            }
            var paramInfo = candidates[0].p;
            if( candidates[0].Item2.IsCommand != expectCommand )
            {
                if( expectCommand )
                {
                    monitor.Error( $"Method {CrisType.MethodName( m, parameters )} must have a ICommand or ICommand<TResult> parameter. Parameter '{paramInfo.Name}' is a IEvent." );
                }
                else
                {
                    monitor.Error( $"Method {CrisType.MethodName( m, parameters )} must have a IEvent parameter. Parameter '{paramInfo.Name}' is a ICommand or ICommand<TResult>." );
                }
                return (null, null, null);
            }
            Throw.DebugAssert( candidates[0].Item2.P != null );
            return (parameters, paramInfo, candidates[0].Item2.P);
        }

        (ParameterInfo[]? Parameters, ParameterInfo? Param, IReadOnlyList<IPrimaryPocoType>? Candidates) GetCandidates( IActivityMonitor monitor,
                                                                                                                        MethodInfo m,
                                                                                                                        bool expectCommands )
        {
            (ParameterInfo[]? parameters, ParameterInfo? p, IPrimaryPocoType? candidate) = TryGetCrisTypeCandidate( monitor, m, expectCommands );
            if( parameters == null ) return (null, null, null);
            if( p != null )
            {
                Throw.DebugAssert( "Since we have the parameter.", candidate != null );
                return (parameters, p, new IPrimaryPocoType[] { candidate });
            }
            // Looking for parts.
            var (param, candidates) = FromPart( monitor, _typeSystem, expectCommands ? _crisCommandTypePart : _crisEventTypePart, m, parameters, expectCommands );
            if( param != null )
            {
                return (parameters, param, candidates);
            }
            return (parameters, null, null);

            static (ParameterInfo? Parameter, IReadOnlyList<IPrimaryPocoType>? Candidates) FromPart( IActivityMonitor monitor,
                                                                                                     IPocoTypeSystem typeSystem,
                                                                                                     IAbstractPocoType? eventOrCommandPartType,
                                                                                                     MethodInfo m,
                                                                                                     ParameterInfo[] parameters,
                                                                                                     bool expectCommands )
            {
                ParameterInfo? pResult = null;
                IReadOnlyList<IPrimaryPocoType>? result = null;
                List<ParameterInfo>? tooMuch = null;
                foreach( var param in parameters )
                {
                    var a = typeSystem.FindByType<IAbstractPocoType>( param.ParameterType );
                    if( a != null )
                    {
                        a = a.NonNullable;
                        if( a.Generalizations.Contains( eventOrCommandPartType ) )
                        {
                            if( pResult == null )
                            {
                                pResult = param;
                                result = a.PrimaryPocoTypes;
                            }
                            else
                            {
                                tooMuch ??= new List<ParameterInfo>();
                                tooMuch.Add( param );
                            }
                        }
                    }
                }
                if( pResult != null && tooMuch == null )
                {
                    return (pResult, result);
                }
                var expected = expectCommands ? "ICommand, ICommand<TResult> or ICommandPart" : "IEvent or IEventPart";
                if( tooMuch != null )
                {
                    Throw.DebugAssert( pResult != null );
                    var t = tooMuch.Select( p => $"{p.ParameterType.ToCSharpName()} {p.Name}" ).Concatenate( "', '" );
                    monitor.Error( $"Method {CrisType.MethodName( m, parameters )} cannot have more than one {expected} parameter. " +
                        $"Found '{pResult.ParameterType.ToCSharpName()} {pResult.Name}', cannot allow '{t}'." );
                }
                else
                {
                    monitor.Error( $"Method {CrisType.MethodName( m, parameters )}: missing a {expected} parameter." );
                }
                return (null, null);
            }


        }

    }

}
