using CK.Core;
using CK.Cris;
using CK.Cris.UbiquitousValues;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

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
        readonly List<(IBaseCompositeType Owner, IBasePocoField Field, bool OwnerHasPostHandler)> _ubiquitousCommandFields;
        // Filled by CloseRegistration.
        readonly HashSet<IPrimaryPocoField> _allUbiquitousFields;

        readonly IPocoTypeSystem _typeSystem;
        readonly IAbstractPocoType? _crisPocoType;
        readonly IAbstractPocoType? _crisCommandType;
        readonly IAbstractPocoType? _crisCommandTypePart;
        readonly IAbstractPocoType? _crisEventType;
        readonly IAbstractPocoType? _crisEventTypePart;
        readonly IPrimaryPocoType? _ubiquitousValues;

        CrisTypeRegistry( IReadOnlyDictionary<IPrimaryPocoType,CrisType> index,
                          IReadOnlyList<CrisType> entries,
                          IPocoTypeSystem typeSystem,
                          IAbstractPocoType? crisPocoType,
                          IAbstractPocoType? crisCommandType,
                          IAbstractPocoType? crisCommandTypePart,
                          IAbstractPocoType? crisEventType,
                          IAbstractPocoType? crisEventTypePart,
                          IPrimaryPocoType? ubiquitousValues )
        {
            _indexedEntries = index;
            _entries = entries;
            _typeSystem = typeSystem;
            _crisPocoType = crisPocoType;
            _crisCommandType = crisCommandType;
            _crisCommandTypePart = crisCommandTypePart;
            _crisEventType = crisEventType;
            _crisEventTypePart = crisEventTypePart;
            _ubiquitousValues = ubiquitousValues;
            _ubiquitousCommandFields = new List<(IBaseCompositeType Owner, IBasePocoField Field, bool OwnerHasPostHandler)>();
            _allUbiquitousFields = new HashSet<IPrimaryPocoField>();
        }

        public IAbstractPocoType? CrisPocoType => _crisPocoType;

        /// <inheritdoc/>
        public IPocoTypeSystem TypeSystem => _typeSystem;

        /// <inheritdoc/>
        public IReadOnlyList<CrisType> CrisTypes => _entries;

        /// <inheritdoc/>
        public CrisType? Find( IPrimaryPocoType poco ) => _indexedEntries.GetValueOrDefault( poco );

        /// <inheritdoc/>
        public bool IsUbiquitousValueField( IPrimaryPocoField field ) => _allUbiquitousFields.Contains( field );

        internal static CrisTypeRegistry? Create( IActivityMonitor monitor, IPocoTypeSystem typeSystem, IStObjEngineMap services )
        {
            bool success = true;
            var entries = new List<CrisType>();
            var indexedEntries = new Dictionary<IPrimaryPocoType, CrisType>();

            IPrimaryPocoType? ubiquitousValues = typeSystem.FindByType<IPrimaryPocoType>( typeof( IUbiquitousValues ) );
            if( ubiquitousValues != null
                && ubiquitousValues.Fields.Any( f => f.Type.IsNullable ) )
            {
                var culprits = ubiquitousValues.Fields.Where( f => f.Type.IsNullable );
                monitor.Error( $"{nameof( IUbiquitousValues )} properties cannot be nullable: {culprits.Select( f => $"{f.Type.CSharpName} {f.Name}" ).Concatenate()}." );
                success = false;
            }

            IPocoGenericTypeDefinition? commandWithResultType = typeSystem.FindGenericTypeDefinition( typeof( ICommand<> ) );

            IAbstractPocoType? crisCommandTypePart = GetAbstractPocoType( monitor, typeSystem, typeof( ICommandPart ) );
            IAbstractPocoType? crisCommandType = crisCommandTypePart?.MinimalGeneralizations.Single()
                                                    ?? GetAbstractPocoType( monitor, typeSystem, typeof( IAbstractCommand ) );

            IAbstractPocoType? crisEventTypePart = GetAbstractPocoType( monitor, typeSystem, typeof( IEventPart ) );
            IAbstractPocoType? crisEventType = crisEventTypePart?.MinimalGeneralizations.Single()
                                                    ?? GetAbstractPocoType( monitor, typeSystem, typeof( IEvent ) );
            IAbstractPocoType? crisPocoType = null;

            if( crisCommandType != null && !crisCommandType.ImplementationLess )
            {
                crisPocoType = crisCommandType.Generalizations.Single();
                foreach( var command in crisCommandType.PrimaryPocoTypes )
                {
                    CrisType? entry = CreateCommandEntry( monitor, commandWithResultType, crisEventType, crisCommandTypePart, services, entries.Count, command );
                    if( entry == null ) success = false;
                    else
                    {
                        entries.Add( entry );
                        indexedEntries.Add( entry.CrisPocoType, entry );
                    }
                }
            }
            // Don't handle events of command handling failed.
            if( !success ) return null;

            int commandCount = entries.Count;
            monitor.Trace( $"Found {entries.Count} Cris commands." );

            if( crisEventType != null && !crisEventType.ImplementationLess )
            {
                crisPocoType ??= crisEventType.Generalizations.Single();
                if( crisEventTypePart != null )
                {
                    foreach( var evPart in crisEventTypePart.AllSpecializations )
                    {
                        success &= CheckNoRoutedOrImmediateAttribute( monitor, evPart.Type );
                    }
                }
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
            if( crisPocoType == null )
            {
                monitor.Warn( $"No Cris commands nor events found." );
            }
            return success
                    ? new CrisTypeRegistry( indexedEntries,
                                            entries,
                                            typeSystem,
                                            crisPocoType,
                                            crisCommandType,
                                            crisCommandTypePart,
                                            crisEventType,
                                            crisEventTypePart,
                                            ubiquitousValues )
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
                                                 IAbstractPocoType? crisCommandTypePart,
                                                 IStObjEngineMap services,
                                                 int crisPocoIndex,
                                                 IPrimaryPocoType command )
            {
                if( crisEventType != null && command.AbstractTypes.Contains( crisEventType ) )
                {
                    monitor.Error( $"Cris '{command}' cannot be both a IEvent and a IAbstractCommand." );
                    return null;
                }
                bool success = CheckCommandTypeNames( monitor, crisCommandTypePart, command );
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

                static bool CheckCommandTypeNames( IActivityMonitor monitor, IAbstractPocoType? crisCommandTypePart, IPrimaryPocoType command )
                {
                    bool success = true;
                    var badCommands = command.SecondaryTypes.OfType<IPocoType>().Prepend( command ).Where( c => !c.CSharpName.EndsWith( "Command", StringComparison.Ordinal ) );
                    if( badCommands.Any() )
                    {
                        monitor.Error( $"Invalid type name for Cris '{badCommands.Select( c => c.ToString()).Concatenate("', '")}': IAbstractCommand type name must end with \"Command\"." );
                        success = false;
                    }
                    if( crisCommandTypePart != null )
                    {
                        var badParts = command.AbstractTypes.Where( p => !p.Type.Name.StartsWith( "ICommand", StringComparison.Ordinal ) && p.Generalizations.Contains( crisCommandTypePart ) );
                        if( badParts.Any() )
                        {
                            monitor.Error( $"Invalid type name for Cris '{badParts.Select( c => c.ToString() ).Concatenate( "', '" )}': ICommandPart type name must start with \"ICommand\"." );
                            success = false;
                        }
                    }
                    return success;
                }
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

            static bool CheckNoRoutedOrImmediateAttribute( IActivityMonitor monitor, Type i )
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

        }

        internal bool CloseRegistration( IActivityMonitor monitor )
        {
            bool success = true;
            if( _ubiquitousCommandFields.Count > 0 )
            {
                var byName = new Dictionary<string, (IBaseCompositeType FirstOwner, IPocoType PropertyType)>();
                foreach( var f in _ubiquitousCommandFields )
                {
                    if( byName.TryGetValue( f.Field.Name, out var already ) )
                    {
                        if( already.PropertyType != f.Field.Type )
                        {
                            monitor.Error( $"[UbiquitousValue] property type '{f.Field.Name}' differ: it is '{already.PropertyType.CSharpName}' for '{already.FirstOwner}' " +
                                           $"and  '{f.Field.Type.CSharpName}' for '{f.Owner.CSharpName}'." );
                            success = false;
                        }
                    }
                    if( !f.OwnerHasPostHandler )
                    {
                        monitor.Warn( $"A [PostHandler] method for '{f.Owner.NonNullable.CSharpName}' has not been found.{Environment.NewLine}" +
                                        $"The [UbiquitousValue] property '{f.Field.Type.CSharpName} {f.Field.Name}' may not be initialized." );
                    }
                    if( f.Field is IAbstractPocoField a )
                    {
                        foreach( var impl in a.Implementations )
                        {

                            _allUbiquitousFields.Add( impl );
                        }
                    }
                    else if( f.Field is IPrimaryPocoField b )
                    {
                        _allUbiquitousFields.Add( b );
                    }
                }
            }
            return success;
        }

        internal void RegisterUbiquitousValueDefinitionField( IBaseCompositeType owner, IBasePocoField field )
        {
            Throw.DebugAssert( !_ubiquitousCommandFields.Any( a => a.Field == field ) );
            _ubiquitousCommandFields.Add( (owner,field,false) );
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
            var idxUbiquitousField = _ubiquitousCommandFields.IndexOf( cf => cf.Owner == e.CrisPocoType );
            if( idxUbiquitousField >= 0 )
            {
                CollectionsMarshal.AsSpan( _ubiquitousCommandFields )[idxUbiquitousField].OwnerHasPostHandler = true;
            }
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

        internal bool RegisterMultiTargetHandler( IActivityMonitor monitor,
                                                   MultiTargetHandlerKind target,
                                                   IStObjFinalClass impl,
                                                   MethodInfo m,
                                                   string? fileName,
                                                   int lineNumber )
        {
            bool expectCommands = target != MultiTargetHandlerKind.RoutedEventHandler;
            if( !GetCandidates( monitor,
                                m,
                                expectCommands,
                                out ParameterInfo[]? parameters,
                                out ParameterInfo? foundParameter,
                                out IReadOnlyList<IPrimaryPocoType>? candidates ) )
            {
                return false;
            }
            bool success = true;
            if( candidates.Count == 0 )
            {
                // If foundParameter is not null, the skipping has already been signaled. 
                if( foundParameter == null )
                {
                    monitor.Info( $"Method {CrisType.MethodName( m, parameters )} is unused since no I{(expectCommands ? "Command" : "Event")}Part exist." );
                }
            }
            else
            {
                Throw.DebugAssert( "We have candidates => we found a parameter (a IEvent or a ICommand or ICommandPart).", foundParameter != null );
                // This check applies to all command based handlers: command handlers and post handlers accept ICrisCommandContext.
                var execContext = parameters.FirstOrDefault( p => p.ParameterType == typeof( ICrisCommandContext ) );
                if( execContext != null )
                {
                    monitor.Error( $"Invalid parameter '{execContext.Name}' in method '{CrisType.MethodName( m, parameters )}': {nameof(ICrisCommandContext)} cannot " +
                                   $"be used in a command [{target}] since it cannot execute commands or send events." );
                    success = false;
                }
                ParameterInfo? messageCollectorOrAmbientServiceHub = null;
                if( target is not MultiTargetHandlerKind.RoutedEventHandler )
                {
                    Throw.DebugAssert( target is MultiTargetHandlerKind.CommandIncomingValidator or MultiTargetHandlerKind.CommandHandlingValidator or MultiTargetHandlerKind.ConfigureAmbientServices );
                    var callContext = parameters.FirstOrDefault( p => p.ParameterType == typeof( ICrisEventContext ) );
                    if( callContext != null )
                    {
                        monitor.Error( $"Invalid parameter '{callContext.Name}' in method '{CrisType.MethodName( m, parameters )}': {nameof(ICrisEventContext)} cannot " +
                                       $"be used in a command [{target}] since it cannot execute commands." );
                        success = false;
                    }
                    messageCollectorOrAmbientServiceHub = parameters.FirstOrDefault( p => p.ParameterType == (target == MultiTargetHandlerKind.ConfigureAmbientServices
                                                                                                                ? typeof( AmbientServiceHub )
                                                                                                                : typeof( UserMessageCollector )) );
                    if( messageCollectorOrAmbientServiceHub == null )
                    {
                        if( target == MultiTargetHandlerKind.ConfigureAmbientServices )
                        {
                            monitor.Error( $"[ConfigureAmbientServices] method '{CrisType.MethodName( m, parameters )}' must take a 'AmbientServiceHub' parameter " +
                                           $"to configure the ambient services." );
                        }
                        else
                        {
                            monitor.Error( $"Command validator method '{CrisType.MethodName( m, parameters )}' must take a 'UserMessageCollector' parameter " +
                                           $"to collect validation errors, warnings and informations." );
                        }
                        success = false;
                    }
                }
                if( success )
                {
                    foreach( var family in candidates )
                    {
                        Throw.DebugAssert( _indexedEntries.ContainsKey( family ), "Since parameters are filtered by registered Poco." );
                        success &= _indexedEntries[family].AddMultiTargetHandler( monitor,
                                                                                  target,
                                                                                  impl,
                                                                                  m,
                                                                                  parameters,
                                                                                  foundParameter,
                                                                                  messageCollectorOrAmbientServiceHub,
                                                                                  fileName,
                                                                                  lineNumber );
                    }
                }
            }
            return success;
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

        // Parameters == null ==> Error.
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

        bool GetCandidates( IActivityMonitor monitor,
                            MethodInfo m,
                            bool expectCommands,
                            [NotNullWhen( true )] out ParameterInfo[]? parameters,
                            out ParameterInfo? foundParameter,
                            [NotNullWhen( true )] out IReadOnlyList<IPrimaryPocoType>? candidates )
        {
            (parameters, foundParameter, IPrimaryPocoType? candidate) = TryGetCrisTypeCandidate( monitor, m, expectCommands );
            if( parameters == null )
            {
                candidates = null;
                return false;
            }
            if( foundParameter != null )
            {
                Throw.DebugAssert( "Since we have the parameter.", candidate != null );
                candidates = new IPrimaryPocoType[] { candidate };
                return true;
            }
            // Looking for parts.
            var partBase = expectCommands ? _crisCommandTypePart : _crisEventTypePart;
            if( partBase != null )
            {
                return FromPart( monitor, _typeSystem, partBase, m, parameters, expectCommands, out foundParameter, out candidates );
            }
            // Not found but it is not an error.
            // If foundParameter is null its because the there is no IEventPart or ICommandPart at all.
            candidates = Array.Empty<IPrimaryPocoType>();
            return true;

            static bool FromPart( IActivityMonitor monitor,
                                  IPocoTypeSystem typeSystem,
                                  IAbstractPocoType eventOrCommandPartType,
                                  MethodInfo m,
                                  ParameterInfo[] parameters,
                                  bool expectCommands,
                                  [NotNullWhen( true )] out ParameterInfo? foundParameter,
                                  [NotNullWhen( true )] out IReadOnlyList<IPrimaryPocoType>? candidates )
            {
                foundParameter = null;
                candidates = null;
                List<ParameterInfo>? tooMuch = null;
                foreach( var param in parameters )
                {
                    var a = typeSystem.FindByType<IAbstractPocoType>( param.ParameterType );
                    bool isTheOne = a != null && a.NonNullable.Generalizations.Contains( eventOrCommandPartType );
                    bool isTheOneEvenDisabled = isTheOne || eventOrCommandPartType.Type.IsAssignableFrom( param.ParameterType );
                    if( isTheOneEvenDisabled )
                    {
                        if( foundParameter == null )
                        {
                            foundParameter = param;
                            if( isTheOne )
                            {
                                Throw.DebugAssert( a != null );
                                candidates = a.NonNullable.PrimaryPocoTypes;
                            }
                            else
                            {
                                monitor.Info( $"Method {CrisType.MethodName( m, parameters )}: parameter '{param.Name}' is " +
                                                $"a 'I{(expectCommands ? "Command" : "Event")}Part' but no command of type '{param.ParameterType:C}' exist. " +
                                                $"It is ignored." );
                                candidates = Array.Empty<IPrimaryPocoType>();
                            }
                        }
                        else
                        {
                            tooMuch ??= new List<ParameterInfo>();
                            tooMuch.Add( param );
                        }
                    }
                }
                // If we found a parameter, candidate may be empty but it is not an error.
                if( foundParameter != null && tooMuch == null )
                {
                    Throw.DebugAssert( candidates != null );
                    return true;
                }
                var expected = expectCommands ? "ICommand, ICommand<TResult> or ICommandPart" : "IEvent or IEventPart";
                if( tooMuch != null )
                {
                    Throw.DebugAssert( foundParameter != null );
                    var t = tooMuch.Select( p => $"{p.ParameterType.ToCSharpName()} {p.Name}" ).Concatenate( "', '" );
                    monitor.Error( $"Method {CrisType.MethodName( m, parameters )} cannot have more than one {expected} parameter. " +
                                    $"Found '{foundParameter.ParameterType.ToCSharpName()} {foundParameter.Name}', cannot allow '{t}'." );
                    return false;
                }
                if( candidates == null ) 
                {
                    monitor.Error( $"Method {CrisType.MethodName( m, parameters )}: missing a {expected} parameter." );
                    return false;
                }
                Throw.DebugAssert( foundParameter != null );
                return true;
            }


        }

    }

}
