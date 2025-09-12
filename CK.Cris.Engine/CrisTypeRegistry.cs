using CK.Core;
using CK.Cris;
using CK.Cris.AmbientValues;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace CK.Setup.Cris;


/// <summary>
/// Internal registry. Instantiated by the <see cref="CrisDirectoryImpl"/> once IPocoTypeSystem is available.
/// Attributes (<see cref="HandlerBase"/>) wait for it to register their handlers.
/// Once done, the public ICrisDirectoryServiceEngine is published so that Cris dependent components can use it.
/// </summary>
internal sealed partial class CrisTypeRegistry : ICrisDirectoryServiceEngine
{
    readonly IReadOnlyDictionary<IPrimaryPocoType, CrisType> _indexedTypes;
    readonly IReadOnlyList<CrisType> _allTypes;
    readonly Dictionary<string, AmbientValueEntry> _ambientValues;
    readonly HashSet<IPrimaryPocoField> _allAmbientValueFields;

    readonly IPocoTypeSystem _typeSystem;
    /// <summary>
    /// <see cref="ICrisPoco"/>
    /// </summary>
    readonly IAbstractPocoType? _crisPocoType;
    /// <summary>
    /// <see cref="IAbstractCommand"/>.
    /// </summary>
    readonly IAbstractPocoType? _crisCommandType;
    /// <summary>
    /// <see cref="ICommandPart"/>.
    /// </summary>
    readonly IAbstractPocoType? _crisCommandTypePart;
    /// <summary>
    /// <see cref="IEvent"/>.
    /// </summary>
    readonly IAbstractPocoType? _crisEventType;
    /// <summary>
    /// <see cref="IEventPart"/>.
    /// </summary>
    readonly IAbstractPocoType? _crisEventTypePart;
    /// <summary>
    /// <see cref="ICrisPocoPart"/>.
    /// </summary>
    readonly IAbstractPocoType? _crisPocoTypePart;
    /// <summary>
    /// <see cref="IAmbientValues"/>.
    /// </summary>
    readonly IPrimaryPocoType? _ambientValuesType;

    CrisTypeRegistry( IReadOnlyDictionary<IPrimaryPocoType, CrisType> index,
                      IReadOnlyList<CrisType> entries,
                      IPocoTypeSystem typeSystem,
                      IAbstractPocoType? crisPocoType,
                      IAbstractPocoType? crisCommandType,
                      IAbstractPocoType? crisCommandTypePart,
                      IAbstractPocoType? crisEventType,
                      IAbstractPocoType? crisEventTypePart,
                      IAbstractPocoType? crisPocoTypePart,
                      IPrimaryPocoType? ambientValuesType )
    {
        _indexedTypes = index;
        _allTypes = entries;
        _typeSystem = typeSystem;
        _crisPocoType = crisPocoType;
        _crisCommandType = crisCommandType;
        _crisCommandTypePart = crisCommandTypePart;
        _crisEventType = crisEventType;
        _crisEventTypePart = crisEventTypePart;
        _crisPocoTypePart = crisPocoTypePart;
        _ambientValuesType = ambientValuesType;
        _ambientValues = new Dictionary<string, AmbientValueEntry>();
        _allAmbientValueFields = new HashSet<IPrimaryPocoField>();
    }

    public IAbstractPocoType? CrisPocoType => _crisPocoType;

    /// <inheritdoc/>
    public IPocoTypeSystem TypeSystem => _typeSystem;

    /// <inheritdoc/>
    public IReadOnlyList<CrisType> CrisTypes => _allTypes;

    /// <inheritdoc/>
    public CrisType? Find( IPrimaryPocoType poco ) => _indexedTypes.GetValueOrDefault( poco );

    /// <inheritdoc/>
    public bool IsAmbientServiceValueField( IPrimaryPocoField field ) => _allAmbientValueFields.Contains( field );

    internal static CrisTypeRegistry? Create( IActivityMonitor monitor, IPocoTypeSystem typeSystem, IStObjEngineMap services )
    {
        bool success = true;
        var entries = new List<CrisType>();
        var indexedEntries = new Dictionary<IPrimaryPocoType, CrisType>();

        IPrimaryPocoType? ambientValuesType = typeSystem.FindByType<IPrimaryPocoType>( typeof( IAmbientValues ) );
        if( ambientValuesType != null
            && ambientValuesType.Fields.Any( f => f.Type.IsNullable ) )
        {
            var culprits = ambientValuesType.Fields.Where( f => f.Type.IsNullable );
            monitor.Error( $"IAmbientValues properties cannot be nullable: {culprits.Select( f => $"{f.Type.CSharpName} {f.Name}" ).Concatenate()}." );
            success = false;
        }

        success &= GetBaseTypesAndCheckNames( monitor,
                                              typeSystem,
                                              out IAbstractPocoType? crisCommandTypePart,
                                              out IAbstractPocoType? crisCommandType,
                                              out IAbstractPocoType? crisEventTypePart,
                                              out IAbstractPocoType? crisEventType,
                                              out IAbstractPocoType? crisPocoTypePart );

        // We need the type definition only if at least a command is found.
        IPocoGenericTypeDefinition? commandWithResultType = null;
        // We'll have a non null crisPocoType only if at least one command or event has been found.
        IAbstractPocoType? crisPocoType = null;

        if( crisCommandType != null && !crisCommandType.ImplementationLess )
        {
            var badNames = crisCommandType.PrimaryPocoTypes.Where( c => !c.CSharpName.EndsWith( "Command", StringComparison.Ordinal ) );
            if( badNames.Any() )
            {
                monitor.Error( $"Invalid type name for Cris '{badNames.Select( c => c.ToString() ).Concatenate( "', '" )}': IAbstractCommand type name must end with \"Command\"." );
                success = false;
            }

            crisPocoType = crisCommandType.Generalizations.Single();
            commandWithResultType = typeSystem.FindGenericTypeDefinition( typeof( ICommand<> ) );
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
            var badNames = crisEventType.PrimaryPocoTypes.Where( c => !c.CSharpName.EndsWith( "Event", StringComparison.Ordinal ) );
            if( badNames.Any() )
            {
                monitor.Error( $"Invalid type name for Cris '{badNames.Select( c => c.ToString() ).Concatenate( "', '" )}': IEvent type name must end with \"Event\"." );
                success = false;
            }

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
            crisPocoTypePart = null;
        }
        else if( crisPocoTypePart == null )
        {
            monitor.Error( "At least one Cris command or event found, ICrisPocoPart cannot be excluded." );
            success = false;
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
                                        crisPocoTypePart,
                                        ambientValuesType )
                : null;

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
            bool success = true;
            CrisPocoKind kind = CrisPocoKind.Command;
            IPocoType? resultType = null;
            if( commandWithResultType != null )
            {
                var withResult = command.AbstractTypes.Where( a => a.GenericTypeDefinition == commandWithResultType );
                var reduced = withResult.ComputeMinimal();
                if( reduced.Count > 1 )
                {
                    monitor.Error( $"""
                        Command '{command}' declares incompatible results '{reduced.Select( a => a.ToString() ).Concatenate( "' ,'" )}'.
                        Result types are incompatible and cannot be reduced.
                        """ );
                    success = false;
                }
                else if( reduced.Count == 1 )
                {
                    resultType = reduced[0].GenericArguments[0].Type;
                    kind = CrisPocoKind.CommandWithResult;
                }
                else
                {
                    Throw.DebugAssert( reduced.Count == 0 );
                    var unregistered = command.AllAbstractTypes.Where( a => a.ImplementationLess && a.GenericTypeDefinition == commandWithResultType )
                                                               .SelectMany( a => a.GenericArguments.Select( g => g.Type.NonNullable ) )
                                                               .Distinct();
                    if( unregistered.Any() )
                    {
                        monitor.Error( $"""
                                        Command '{command}' has at least one unregistered type:
                                        {unregistered.Select( t => t.ToString() ).Concatenate()}.
                                        """ );
                        success = false;
                    }
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

        static bool GetBaseTypesAndCheckNames( IActivityMonitor monitor,
                                               IPocoTypeSystem typeSystem,
                                               out IAbstractPocoType? crisCommandTypePart,
                                               out IAbstractPocoType? crisCommandType,
                                               out IAbstractPocoType? crisEventTypePart,
                                               out IAbstractPocoType? crisEventType,
                                               out IAbstractPocoType? crisPocoTypePart )
        {
            bool success = true;
            crisCommandTypePart = GetAbstractPocoType( monitor, typeSystem, typeof( ICommandPart ) );
            if( crisCommandTypePart != null )
            {
                var badNames = crisCommandTypePart.AllSpecializations.Where( c => !c.Type.Name.StartsWith( "ICommand", StringComparison.Ordinal ) );
                if( badNames.Any() )
                {
                    monitor.Error( $"Invalid type name for Cris '{badNames.Select( c => c.ToString() ).Concatenate( "', '" )}': ICommandPart type name must start with \"ICommand\"." );
                    success = false;
                }
            }
            crisCommandType = GetAbstractPocoType( monitor, typeSystem, typeof( IAbstractCommand ) );

            crisEventTypePart = GetAbstractPocoType( monitor, typeSystem, typeof( IEventPart ) );
            if( crisEventTypePart != null )
            {
                var badNames = crisEventTypePart.AllSpecializations.Where( c => !c.Type.Name.StartsWith( "IEvent", StringComparison.Ordinal ) );
                if( badNames.Any() )
                {
                    monitor.Error( $"Invalid type name for Cris '{badNames.Select( c => c.ToString() ).Concatenate( "', '" )}': IEventPart type name must start with \"IEvent\"." );
                    success = false;
                }
            }
            crisEventType = GetAbstractPocoType( monitor, typeSystem, typeof( IEvent ) );

            crisPocoTypePart = typeSystem.FindByType<IAbstractPocoType>( typeof( ICrisPocoPart ) );
            if( crisPocoTypePart != null )
            {
                crisPocoTypePart = crisPocoTypePart.NonNullable;
                var cmdPart = crisCommandType;
                var eventPart = crisEventType;
                var badNames = crisPocoTypePart.AllSpecializations.Where( c => !c.Type.Name.EndsWith( "Part", StringComparison.Ordinal )
                                                                                  && !c.Generalizations.Any( g => g == cmdPart || g == eventPart ) );
                if( badNames.Any() )
                {
                    monitor.Error( $"Invalid type name for Cris '{badNames.Select( c => c.ToString() ).Concatenate( "', '" )}': ICrisPocoPart type name must end with \"Part\"." );
                    success = false;
                }
            }

            return success;

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

        }
    }

    internal bool RegisterCommandHandler( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m, bool allowUnclosed, string? fileName, int lineNumber )
    {
        (CrisType? e, ParameterInfo[]? parameters, ParameterInfo? p) = GetSingleCommandEntry( monitor, "Handler", m );
        if( e == null ) return parameters != null;
        Throw.DebugAssert( parameters != null && p != null );
        bool isClosedHandler = p.ParameterType == e.CrisPocoType.FamilyInfo.ClosureInterface;
        if( !isClosedHandler && !allowUnclosed )
        {

            allowUnclosed = p.GetCustomAttributes().Any( a => a.GetType().FindInterfaces( ( i, n ) => i.Name == (string?)n, nameof( IAllowUnclosedCommandAttribute ) ).Length > 0 );
            if( !allowUnclosed )
            {
                // If the Command is not the closure, we skip handlers of unclosed commands: we expect the final handler of the closure interface.
                monitor.Info( $"""
                    Method {CrisType.MethodName( m, parameters )} cannot handle '{e.PocoName}' command because type '{p.ParameterType.Name}' doesn't represent the whole command.
                    If this method can actually handle the whole command, specify [CommandHandler( AllowUnclosedCommand = true )].
                    """ );
                return true;
            }
        }
        return e.AddCommandHandler( monitor, _typeSystem, impl, m, parameters, p, isClosedHandler, fileName, lineNumber );
    }

    internal bool RegisterCommandPostHandler( IActivityMonitor monitor, IStObjFinalClass impl, MethodInfo m, string? fileName, int lineNumber )
    {
        (CrisType? e, ParameterInfo[]? parameters, ParameterInfo? p) = GetSingleCommandEntry( monitor, "PostHandler", m );
        if( e == null ) return parameters != null;
        Throw.DebugAssert( parameters != null && p != null );
        return e.AddPostHandler( monitor, _typeSystem, impl, m, parameters, p, fileName, lineNumber );
    }

    // e == null && parameters == null => error
    // e == null && parameters != null => warning (Method skipped: No IAbstractCommand parameter detected.)
    (CrisType? e, ParameterInfo[]? parameters, ParameterInfo? p) GetSingleCommandEntry( IActivityMonitor monitor, string kind, MethodInfo m )
    {
        (ParameterInfo[]? parameters, ParameterInfo? p, IPrimaryPocoType? candidate) = TryGetCrisTypeCandidate( monitor, m, ExpectedParamType.IAbstractCommand );
        if( parameters == null )
        {
            return (null, null, null);
        }
        if( p == null )
        {
            monitor.Warn( $"Skipping {kind} method {CrisType.MethodName( m, parameters )}: no IAbstractCommand parameter detected." );
            return (null, parameters, null);
        }
        Throw.DebugAssert( "Since we have a parameter.", candidate != null );
        Throw.DebugAssert( "Since parameters are filtered by registered Poco.", _indexedTypes.ContainsKey( candidate ) );
        return (_indexedTypes[candidate], parameters, p);
    }

    enum ExpectedParamType
    {
        ICrisPoco,
        IAbstractCommand,
        IEvent
    }

    static string ExpectedParamTypeDetail( ExpectedParamType t ) => t switch
    {
        ExpectedParamType.IAbstractCommand => "ICommand, ICommand<TResult>, ICommandPart",
        ExpectedParamType.IEvent => "IEvent, IEventPart",
        _ => ""
    };

    internal bool RegisterMultiTargetHandler( IActivityMonitor monitor,
                                              MultiTargetHandlerKind target,
                                              IStObjMap engineMap,
                                              IStObjFinalClass impl,
                                              MethodInfo m,
                                              string? fileName,
                                              int lineNumber )
    {
        var expectedParamType = target switch
        {
            MultiTargetHandlerKind.ConfigureAmbientServices => ExpectedParamType.ICrisPoco,
            MultiTargetHandlerKind.RestoreAmbientServices => ExpectedParamType.ICrisPoco,
            MultiTargetHandlerKind.IncomingValidator => ExpectedParamType.ICrisPoco,
            MultiTargetHandlerKind.CommandHandlingValidator => ExpectedParamType.IAbstractCommand,
            _ => ExpectedParamType.IEvent // MultiTargetHandlerKind.RoutedEventHandler.
        };

        if( !GetCandidates( monitor,
                            m,
                            expectedParamType,
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
                monitor.Info( $"Method {CrisType.MethodName( m, parameters )} is unused since no {expectedParamType}Part exist." );
            }
        }
        else
        {
            Throw.DebugAssert( "We have candidates => we found a parameter (a IEvent, ICommand or a part).", foundParameter != null );
            if( target is MultiTargetHandlerKind.RestoreAmbientServices
                && impl.IsScoped )
            {
                monitor.Error( $"Invalid [RestoreAmbientServices] method in '{impl.ClassType:C}': this is a scoped service. Only singletons can be used to restore ambient services." );
                success = false;
            }
            // Loop once on the parameters for all the kind.
            ParameterInfo? argumentParameter = null;
            ParameterInfo? argumentParameter2 = null;
            Type? expectedArgumentType = target switch
            {
                MultiTargetHandlerKind.ConfigureAmbientServices => typeof( AmbientServiceHub ),
                MultiTargetHandlerKind.RestoreAmbientServices => typeof( AmbientServiceHub ),
                MultiTargetHandlerKind.IncomingValidator => typeof( UserMessageCollector ),
                MultiTargetHandlerKind.CommandHandlingValidator => typeof( UserMessageCollector ),
                _ => null // MultiTargetHandlerKind.RoutedEventHandler.
            };
            Type? expectedArgumentType2 = target is MultiTargetHandlerKind.IncomingValidator ? typeof( ICrisIncomingValidationContext ) : null;

            foreach( var p in parameters )
            {
                if( p == foundParameter ) continue;
                success &= CheckExpectedSpecificArgument( monitor, target, expectedArgumentType, m, p, parameters, ref argumentParameter );
                success &= CheckExpectedSpecificArgument( monitor, target, expectedArgumentType2, m, p, parameters, ref argumentParameter2 );
                // This check applies to all kind here.
                // Only command handlers and post handlers (that are not MultiTargetHandlers) accept ICrisCommandContext.
                if( p.ParameterType == typeof( ICrisCommandContext ) )
                {
                    monitor.Error( $"[{target}] method '{CrisType.MethodName( m, parameters )}': invalid parameter '{p.Name}'. {nameof( ICrisCommandContext )} cannot be used in a [{target}] since it cannot execute commands or send events." );
                    success = false;
                    continue;
                }
                if( target is not MultiTargetHandlerKind.RoutedEventHandler )
                {
                    Throw.DebugAssert( target is MultiTargetHandlerKind.IncomingValidator
                                                or MultiTargetHandlerKind.CommandHandlingValidator
                                                or MultiTargetHandlerKind.ConfigureAmbientServices
                                                or MultiTargetHandlerKind.RestoreAmbientServices );

                    if( p.ParameterType == typeof( ICrisEventContext ) )
                    {
                        monitor.Error( $"[{target}] method '{CrisType.MethodName( m, parameters )}': invalid parameter '{p.Name}'. {nameof( ICrisEventContext )} cannot be used in a [{target}] since it cannot execute commands." );
                        success = false;
                    }
                    if( target is MultiTargetHandlerKind.RestoreAmbientServices )
                    {
                        // [RestoreAmbientServices] is the more restrictive: no scoped except the IActivityMonitor
                        // and the AmbientServiceHub.
                        if( p.ParameterType != typeof( IActivityMonitor )
                            && p.ParameterType != typeof( AmbientServiceHub )
                            && engineMap.ToLeaf( p.ParameterType )?.IsScoped is not false )
                        {
                            monitor.Error( $"[{target}] method '{CrisType.MethodName( m, parameters )}': invalid parameter '{p.Name}'. Only singleton services can be used to restore ambient services." );
                            success = false;
                        }
                    }
                }
            }
            if( expectedArgumentType != null )
            {
                if( argumentParameter == null && (expectedArgumentType2 == null || argumentParameter2 == null) )
                {
                    if( target is MultiTargetHandlerKind.ConfigureAmbientServices or MultiTargetHandlerKind.RestoreAmbientServices )
                    {
                        monitor.Error( $"[{target}] method '{CrisType.MethodName( m, parameters )}' must take a 'AmbientServiceHub' parameter to configure the ambient services." );
                    }
                    else if( target is MultiTargetHandlerKind.CommandHandlingValidator )
                    {
                        monitor.Error( $"[{target}] method '{CrisType.MethodName( m, parameters )}' must take a 'UserMessageCollector' parameter to collect validation errors, warnings and informations." );
                    }
                    else
                    {
                        Throw.DebugAssert( target is MultiTargetHandlerKind.IncomingValidator );
                        monitor.Error( $"[{target}] method '{CrisType.MethodName( m, parameters )}' must take a 'UserMessageCollector' and/or a 'ICrisIncomingValidationContext' parameter to collect validation errors, warnings and informations." );
                    }
                    success = false;
                }
            }
            if( success )
            {
                foreach( var family in candidates )
                {
                    Throw.DebugAssert( _indexedTypes.ContainsKey( family ), "Since parameters are filtered by registered Poco." );
                    success &= _indexedTypes[family].AddMultiTargetHandler( monitor,
                                                                            target,
                                                                            impl,
                                                                            m,
                                                                            parameters,
                                                                            foundParameter,
                                                                            argumentParameter,
                                                                            argumentParameter2,
                                                                            fileName,
                                                                            lineNumber );
                }
            }
        }
        return success;

        static bool CheckExpectedSpecificArgument( IActivityMonitor monitor,
                                                   MultiTargetHandlerKind target,
                                                   Type? expectedArgumentType,
                                                   MethodInfo m,
                                                   ParameterInfo p,
                                                   ParameterInfo[] parameters,
                                                   ref ParameterInfo? argumentParameter )
        {
            if( p.ParameterType == expectedArgumentType )
            {
                if( argumentParameter != null )
                {
                    monitor.Error( $"[{target}] method '{CrisType.MethodName( m, parameters )}' has duplicate parameter '{p.Name}' and '{argumentParameter.Name}'." );
                    return false;
                }
                argumentParameter = p;
            }
            return true;
        }
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
                                                                                                              ExpectedParamType expected )
    {
        var parameters = m.GetParameters();
        var candidates = parameters.Select( p => (p, GetPrimary( p.ParameterType )) )
                                   .Where( x => x.Item2.P != null )
                                   .ToArray();
        if( candidates.Length > 1 )
        {
            string tooMuch = candidates.Select( c => $"{c.p.ParameterType.ToCSharpName()} {c.p.Name}" ).Concatenate();
            var type = expected switch { ExpectedParamType.IAbstractCommand => "command", ExpectedParamType.IEvent => "event", _ => "command or event" };
            monitor.Error( $"Method {CrisType.MethodName( m, parameters )} cannot have more than one concrete {type} parameter. Found: {tooMuch}" );
            return (null, null, null);
        }
        if( candidates.Length == 0 )
        {
            return (parameters, null, null);
        }
        var paramInfo = candidates[0].p;
        if( expected != ExpectedParamType.ICrisPoco && candidates[0].Item2.IsCommand != expected is ExpectedParamType.IAbstractCommand )
        {
            if( expected is ExpectedParamType.IAbstractCommand )
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
                        ExpectedParamType expected,
                        [NotNullWhen( true )] out ParameterInfo[]? parameters,
                        out ParameterInfo? foundParameter,
                        [NotNullWhen( true )] out IReadOnlyList<IPrimaryPocoType>? candidates )
    {
        (parameters, foundParameter, IPrimaryPocoType? candidate) = TryGetCrisTypeCandidate( monitor, m, expected );
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
        // If we expect a command or an event but the ICommand/EventPart is null (there is no such part), we lookup
        // the ICrisPocoPart to handle any definition error.
        var partBase = expected switch { ExpectedParamType.IAbstractCommand => _crisCommandTypePart, ExpectedParamType.IEvent => _crisEventTypePart, _ => null }
                       ?? _crisPocoTypePart;
        if( partBase != null )
        {
            Throw.DebugAssert( "command or event part => ICrisPoco => ICrisPocoPart", _crisPocoTypePart != null );
            return FromPart( monitor, _typeSystem, partBase, _crisPocoTypePart, m, parameters, expected, out foundParameter, out candidates );
        }
        // Not found but it is not an error: there is no adhoc type to handle.
        candidates = Array.Empty<IPrimaryPocoType>();
        return true;

        static bool FromPart( IActivityMonitor monitor,
                              IPocoTypeSystem typeSystem,
                              IAbstractPocoType eventOrCommandPartType,
                              IAbstractPocoType crisPocoTypePart,
                              MethodInfo method,
                              ParameterInfo[] parameters,
                              ExpectedParamType expected,
                              out ParameterInfo? foundParameter,
                              [NotNullWhen( true )] out IReadOnlyList<IPrimaryPocoType>? candidates )
        {
            foundParameter = null;
            candidates = null;
            List<ParameterInfo>? tooMany = null;
            foreach( var parameter in parameters )
            {
                AnalyzePocoTypeParameter( monitor,
                                          typeSystem,
                                          eventOrCommandPartType,
                                          crisPocoTypePart,
                                          method,
                                          parameters,
                                          expected,
                                          ref foundParameter,
                                          ref candidates,
                                          ref tooMany,
                                          parameter );
            }
            // If we found a parameter, candidates may be empty but it is not an error.
            // But too many parameters is always an error.
            if( foundParameter != null && tooMany == null )
            {
                Throw.DebugAssert( candidates != null );
                return true;
            }
            if( tooMany != null )
            {
                Throw.DebugAssert( foundParameter != null );
                var t = tooMany.Select( p => $"{p.ParameterType.ToCSharpName()} {p.Name}" ).Concatenate( "', '" );
                monitor.Error( $"Method {CrisType.MethodName( method, parameters )} cannot have more than one {ExpectedParamTypeDetail( expected )} or ICrisPocoPart parameter. " +
                                $"Found '{foundParameter.ParameterType.ToCSharpName()} {foundParameter.Name}', cannot allow '{t}'." );
                return false;
            }
            if( foundParameter == null || candidates == null )
            {
                monitor.Warn( $"Method {CrisType.MethodName( method, parameters )} misses an existing {ExpectedParamTypeDetail( expected )} or ICrisPocoPart parameter." );
                candidates = Array.Empty<IPrimaryPocoType>();
                return true;
            }
            Throw.DebugAssert( foundParameter != null && candidates.Count > 0 );
            return true;

            static void AnalyzePocoTypeParameter( IActivityMonitor monitor,
                                                  IPocoTypeSystem typeSystem,
                                                  IAbstractPocoType eventOrCommandPartType,
                                                  IAbstractPocoType crisPocoTypePart,
                                                  MethodInfo m,
                                                  ParameterInfo[] parameters,
                                                  ExpectedParamType expected,
                                                  ref ParameterInfo? foundParameter,
                                                  ref IReadOnlyList<IPrimaryPocoType>? candidates,
                                                  ref List<ParameterInfo>? tooMany,
                                                  ParameterInfo parameter )
            {
                var pocoType = typeSystem.FindByType<IAbstractPocoType>( parameter.ParameterType );
                bool isTheOne = pocoType != null && (pocoType.NonNullable.Generalizations.Contains( eventOrCommandPartType )
                                                     || (eventOrCommandPartType != crisPocoTypePart && pocoType.NonNullable.Generalizations.Contains( crisPocoTypePart )));
                // If we didn't find it, it may be a compliant parameter.
                // If it is and we already have a foundParameter => tooMany.
                // If it is and we have no foundParameter yet, we consider it the foundParameter but with an empty candidates list.
                bool mayBeTheOne = isTheOne || eventOrCommandPartType.Type.IsAssignableFrom( parameter.ParameterType );
                bool mayBeTheOne2 = mayBeTheOne || (eventOrCommandPartType != crisPocoTypePart && crisPocoTypePart.Type.IsAssignableFrom( parameter.ParameterType ));
                if( mayBeTheOne2 )
                {
                    if( foundParameter == null )
                    {
                        foundParameter = parameter;
                        if( isTheOne )
                        {
                            Throw.DebugAssert( pocoType != null );
                            candidates = pocoType.NonNullable.PrimaryPocoTypes;
                        }
                        else
                        {
                            if( mayBeTheOne && eventOrCommandPartType != crisPocoTypePart )
                            {
                                Throw.DebugAssert( expected is not ExpectedParamType.ICrisPoco );
                                var typ = expected is ExpectedParamType.IAbstractCommand ? "Command" : "Event";
                                monitor.Info( $"Method {CrisType.MethodName( m, parameters )}: parameter '{parameter.Name}' is " +
                                            $"a 'I{typ}Part' but no {typ} of type '{parameter.ParameterType:C}' exist. " +
                                            $"It is ignored." );
                            }
                            else
                            {
                                monitor.Info( $"Method {CrisType.MethodName( m, parameters )}: parameter '{parameter.Name}' is " +
                                            $"a 'ICrisPocoPart' but no ICrisPocoPart of type '{parameter.ParameterType:C}' exist. " +
                                            $"It is ignored." );
                            }
                            candidates = Array.Empty<IPrimaryPocoType>();
                        }
                    }
                    else
                    {
                        tooMany ??= new List<ParameterInfo>();
                        tooMany.Add( parameter );
                    }
                }
            }
        }


    }

}
