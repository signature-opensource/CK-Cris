using CK.CodeGen;
using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Code generator of the <see cref="CrisDirectory"/> service.
    /// </summary>
    public partial class CrisDirectoryImpl : CSCodeGeneratorType, IAttributeContextBoundInitializer
    {
        // Auto registers IAmbientValues, ICrisResultError so that tests don't have to register them explicitly: registering CrisDirectory is enough.
        void IAttributeContextBoundInitializer.Initialize( IActivityMonitor monitor, ITypeAttributesCache owner, MemberInfo m, Action<Type> alsoRegister )
        {
            alsoRegister( typeof( ICrisPocoPart ) );
            alsoRegister( typeof( CK.Cris.AmbientValues.IAmbientValues ) );
            alsoRegister( typeof( ICrisResultError ) );
        }

        /// <inheritdoc />
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            // Skips the purely unified BinPath.
            if( c.CurrentRun.ConfigurationGroup.IsUnifiedPure ) return CSCodeGenerationResult.Success;
            // Waits for the IPocoTypeSystem.
            return new CSCodeGenerationResult( nameof( WaitForPocoTypeSystem ) );
        }

        CSCodeGenerationResult WaitForPocoTypeSystem( IActivityMonitor monitor, ICSCodeGenerationContext c, [WaitFor] IPocoTypeSystem typeSystem )
        {
            using( monitor.OpenInfo( $"IPocoTypeSystem is available: discovering Cris Poco types." ) )
            {
                var registry = CrisTypeRegistry.Create( monitor, typeSystem, c.CurrentRun.EngineMap );
                if( registry == null ) return CSCodeGenerationResult.Failed;

                // Expose the internal CrisTypeRegistry type. Attribute handlers use it to register the handlers.
                c.CurrentRun.ServiceContainer.Add( registry );
                // One more step to let the attributes register their handlers.
                // Once done, the public ICrisDirectoryServiceEngine will be published.
                return new CSCodeGenerationResult( nameof( DoImplement ) );
            }
        }


        CSCodeGenerationResult DoImplement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope, CrisTypeRegistry registry )
        {
            Throw.CheckState( "Applies only to the CrisDirectory class.", classType == typeof( CrisDirectory ) );

            if( !CheckICommandHandlerMissingImplementations( monitor, registry ) )
            {
                return CSCodeGenerationResult.Failed;
            }

            scope.Workspace.Global.FindOrCreateNamespace( "CK.Cris" )
                                   .CreateType( "sealed class CrisHandlerDescriptor : CK.Cris.ICrisPocoModel.IHandler" )
                                   .Append( """
                                            public CrisHandlerDescriptor( IStObjFinalClass type,
                                                                          string methodName,
                                                                          Type[] parameters,
                                                                          CK.Cris.CrisHandlerKind kind,
                                                                          string fileName,
                                                                          int lineNumber )
                                            {
                                                Type = type;
                                                MethodName = methodName;
                                                Parameters = parameters;
                                                Kind = kind;
                                                FileName = fileName;
                                                LineNumber = lineNumber;
                                            }

                                            public IStObjFinalClass Type { get; }

                                            public string MethodName { get; }

                                            public CK.Cris.CrisHandlerKind Kind { get; }

                                            public IReadOnlyList<Type> Parameters { get; }

                                            public string FileName { get; }

                                            public int LineNumber { get; }
                                            """ );
            using var scopeRegion = scope.Region();

            scope.Append( "public " ).Append( scope.Name ).Append( "() : base( CreateCommands() ) {}" ).NewLine();

            scope.Append( "static IReadOnlyList<CK.Cris.ICrisPocoModel> CreateCommands()" )
                 .OpenBlock()
                 .Append( "var list = new CK.Cris.ICrisPocoModel[]" ).NewLine()
                 .Append( "{" ).NewLine();
            // Before using the CrisTypes and exposing the ICrisDirectoryServiceEngine:
            //  - Analyze the AmbientServiceValues.
            //  - sets, for each of them, the handler lists to an empty list if they are null (or useless because the CrisType is not handled).
            if( !registry.SettleAmbientValues( monitor ) )
            {
                return CSCodeGenerationResult.Failed;
            }
            foreach( var e in registry.CrisTypes )
            {
                bool isCommand = e.Kind is CrisPocoKind.Command or CrisPocoKind.CommandWithResult;
                e.CloseRegistration( monitor );
                var classScope = scope.Namespace.FindOrCreateAutoImplementedClass( monitor, e.CrisPocoType.FamilyInfo.PocoFactoryClass );
                using( classScope.Region() )
                {
                    classScope.Definition.BaseTypes.Add( new ExtendedTypeName( isCommand ? "CK.Cris.ICrisCommandModel" : "CK.Cris.ICrisPocoModel" ) );
                    classScope.Append( "public Type CommandType => PocoClassType;" ).NewLine()
                     .Append( "public int CrisPocoIndex => " ).Append( e.CrisPocoIndex ).Append( ";" ).NewLine()
                     .Append( "public string PocoName => Name;" ).NewLine()
                     .Append( "public CK.Cris.CrisPocoKind Kind => " ).Append( e.Kind ).Append( ";" ).NewLine()
                     .Append( "public Type ResultType => " ).AppendTypeOf( e.CommandResultType?.Type ?? typeof( void ) ).Append( ";" ).NewLine()
                     .Append( "CK.Cris.ICrisPoco CK.Cris.ICrisPocoModel.Create() => (CK.Cris.ICrisPoco)Create();" ).NewLine()
                     .Append( "public bool IsHandled => " ).Append( e.IsHandled ).Append( ";" ).NewLine()
                     .Append( "public bool EndpointMustConfigureServices => " ).Append( e.AmbientServicesConfigurators.Count > 0 ).Append( ";" ).NewLine()
                     .Append( "public bool BackgroundMustRestoreServices => " ).Append( e.AmbientServicesRestorers.Count > 0 ).Append( ";" ).NewLine();

                    // ImmutableArray<string> AmbientValuePropertyNames
                    if( e.AmbientValueFields.Count > 0 )
                    {
                        classScope.Append( "static readonly ImmutableArray<string> _ambientValues = ImmutableArray.Create( " )
                         .AppendArray( e.AmbientValueFields.Select( f => f.Name ) ).Append( " );" ).NewLine()
                         .Append( "public ImmutableArray<string> AmbientValuePropertyNames => _ambientValues;" ).NewLine();
                    }
                    else
                    {
                        classScope.Append( "public ImmutableArray<string> AmbientValuePropertyNames => ImmutableArray<string>.Empty;" ).NewLine();
                    }

                    // ImmutableArray<CK.Cris.ICrisPocoModel.IHandler> Handlers
                    // Follow the logical order of the handlers.
                    IEnumerable<HandlerBase> allHandlers = ((IEnumerable<HandlerBase>)e.IncomingValidators)
                                                            .Concat( e.HandlingValidators );
                    if( e.CommandHandler != null ) allHandlers = allHandlers.Append( e.CommandHandler );
                    allHandlers = allHandlers.Concat( e.PostHandlers )
                                             .Concat( e.EventHandlers )!;

                    if( allHandlers.Any() )
                    {
                        classScope.Append( "static readonly ImmutableArray<CK.Cris.ICrisPocoModel.IHandler> _handlers = ImmutableArray.Create( new CK.Cris.ICrisPocoModel.IHandler[] {" ).NewLine();
                        foreach( var handler in allHandlers )
                        {
                            classScope.Append( "new CK.Cris.CrisHandlerDescriptor(" ).NewLine()
                             .Append( "CK.StObj.GeneratedRootContext.ToLeaf( " ).AppendTypeOf( handler.Owner.ClassType ).Append( " )," ).NewLine()
                             .AppendSourceString( handler.Method.Name ).Append( "," ).NewLine()
                             .AppendArray( handler.Parameters.Select( p => p.ParameterType ) ).Append( "," )
                             .Append( handler.Kind ).Append( "," )
                             .AppendSourceString( handler.FileName ).Append( "," )
                             .Append( handler.LineNumber )
                             .Append( ")," ).NewLine();
                        }
                        classScope.Append( "} );" ).NewLine()
                         .Append( "public ImmutableArray<CK.Cris.ICrisPocoModel.IHandler> Handlers => _handlers;" );
                    }
                    else
                    {
                        classScope.Append( "public ImmutableArray<CK.Cris.ICrisPocoModel.IHandler> Handlers => ImmutableArray<CK.Cris.ICrisPocoModel.IHandler>.Empty;" );
                    }
                    classScope.NewLine();

                    if( isCommand )
                    {
                        classScope.Append( "public CK.Cris.ExecutedCommand CreateExecutedCommand( CK.Cris.IAbstractCommand command, object? r, ImmutableArray<UserMessage> v, ImmutableArray<CK.Cris.IEvent> e, CK.Cris.IDeferredCommandExecutionContext d )" )
                            .OpenBlock()
                            .Append( "Throw.CheckArgument( command?.CrisPocoModel == this );" ).NewLine()
                            .Append( "return new CK.Cris.ExecutedCommand<" ).Append( e.CrisPocoType.CSharpName ).Append( ">( (" ).Append( e.CrisPocoType.CSharpName ).Append( ")command, r, v, e, d );" )
                            .CloseBlock();
                    }
                }

                // The CrisPocoModel is the _factory field.
                var p = scope.Namespace.FindOrCreateAutoImplementedClass( monitor, e.CrisPocoType.FamilyInfo.PocoClass );
                p.GeneratedByComment().NewLine();
                if( isCommand )
                {
                    p.Append( "CK.Cris.ICrisPocoModel CK.Cris.ICrisPoco.CrisPocoModel => _factory;" ).NewLine()
                     .Append( "public CK.Cris.ICrisCommandModel CrisPocoModel => _factory;" ).NewLine();
                }
                else
                {
                    p.Append( "public CK.Cris.ICrisPocoModel CrisPocoModel => _factory;" ).NewLine();
                }
                scope.Append( p.FullName ).Append( "._factory,").NewLine();
            }
            scope.Append( "};" ).NewLine()
                 .Append( "return list;" )
                 .CloseBlock();

            // Expose the public ICrisDirectoryServiceEngine.
            c.CurrentRun.ServiceContainer.Add<ICrisDirectoryServiceEngine>( registry );

            return CSCodeGenerationResult.Success;
        }

        static bool CheckICommandHandlerMissingImplementations( IActivityMonitor monitor, CrisTypeRegistry registry )
        {
            var success = true;
            var missingHandlers = registry.CrisTypes.Where( c => c.CommandHandler == null );
            foreach( var c in missingHandlers )
            {
                if( c.ExpectedHandlerService != null )
                {
                    if( c.CrisPocoType.FamilyInfo.ClosureInterface != null )
                    {
                        monitor.Error( $"Service '{c.ExpectedHandlerService.ClassType:N}' must implement a command handler method for closed command {c.PocoName} of the closing type {c.CrisPocoType.FamilyInfo.ClosureInterface:N}." );
                    }
                    else
                    {
                        monitor.Error( $"Service '{c.ExpectedHandlerService.ClassType:N}' must implement a command handler method for unclosed command {c.PocoName} of primary type {c.CrisPocoType.FamilyInfo.PrimaryInterface.PocoInterface:N}." );
                    }
                    success = false;
                }
            }
            return success;
        }
    }
}
