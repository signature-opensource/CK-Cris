using CK.CodeGen;
using CK.Core;
using CK.Cris;
using CK.Setup.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Code generator of the <see cref="CrisDirectory"/> service.
    /// </summary>
    public partial class CrisDirectoryImpl : CSCodeGeneratorType, IAttributeContextBoundInitializer
    {

        // Auto registers IAmbientValues, ICrisResultError and IAspNetCrisResultError so that tests don't have to register them explicitly: registering CrisDirectory is enough.
        void IAttributeContextBoundInitializer.Initialize( IActivityMonitor monitor, ITypeAttributesCache owner, MemberInfo m, Action<Type> alsoRegister )
        {
            alsoRegister( typeof( CK.Cris.AmbientValues.IAmbientValues ) );
            alsoRegister( typeof( ICrisResultError ) );
        }

        // We keep a reference instead of using CommandRegistry.FindOrCreate each time (for TypeScript).
        CrisRegistry? _registry;

        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            // We need the JsonSerializationCodeGen service to register command result type.
            return new CSCodeGenerationResult( nameof( DoImplement ) );
        }

        CSCodeGenerationResult DoImplement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope, JsonSerializationCodeGen? json = null )
        { 
            Throw.CheckState( "Applies only to the CrisDirectory class.", classType == typeof( CrisDirectory ) );
            _registry = CrisRegistry.FindOrCreate( monitor, c );
            if( _registry == null ) return CSCodeGenerationResult.Failed;

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
            foreach( var e in _registry.CrisPocoModels )
            {
                // Registering non Poco result type (Poco are all registered by JsonSerializationCodeGen).
                if( json != null
                    && e.ResultType != typeof(void)
                    && !json.IsAllowedType( e.ResultType ) )
                {
                    if( !json.AllowType( e.ResultNullableTypeTree ) )
                    {
                        monitor.Error( $"Failed to allow returned type '{e.ResultNullableTypeTree}' in JSON for command '{e.PocoName}'." );
                        return CSCodeGenerationResult.Failed;
                    }
                }
                var f = scope.Namespace.FindOrCreateAutoImplementedClass( monitor, e.CrisPocoInfo.PocoFactoryClass );
                using( f.Region() )
                {
                    f.Definition.BaseTypes.Add( new ExtendedTypeName( "CK.Cris.ICrisPocoModel" ) );
                    f.Append( "public Type CommandType => PocoClassType;" ).NewLine()
                     .Append( "public int CrisPocoIndex => " ).Append( e.CrisPocoIndex ).Append( ";" ).NewLine()
                     .Append( "public string PocoName => Name;" ).NewLine()
                     .Append( "public CK.Cris.CrisPocoKind Kind => " ).Append( e.Kind ).Append( ";" ).NewLine()
                     .Append( "public Type ResultType => " ).AppendTypeOf( e.ResultType ).Append( ";" ).NewLine()
                     .Append( "CK.Cris.ICrisPoco CK.Cris.ICrisPocoModel.Create() => (CK.Cris.ICrisPoco)Create();" ).NewLine();

                    if( !e.IsHandled )
                    {
                        f.Append( "public bool IsHandled => false;" ).NewLine()
                         .Append( "public IReadOnlyList<CK.Cris.ICrisPocoModel.IHandler> Handlers => Array.Empty<CK.Cris.ICrisPocoModel.IHandler>();" );
                    }
                    else
                    {
                        var allHandlers = e.CommandHandler != null
                                            ? ((IEnumerable<CrisRegistry.BaseHandler>)e.Validators).Append( e.CommandHandler ).Concat( e.PostHandlers )
                                            : e.EventHandlers;
                        Throw.DebugAssert( allHandlers.Any() );

                        f.Append( "static readonly CK.Cris.ICrisPocoModel.IHandler[] _handlers = new [] {" ).NewLine();
                        foreach( var handler in allHandlers )
                        {
                            f.Append( "new CK.Cris.CrisHandlerDescriptor(" ).NewLine()
                             .Append( "CK.StObj.GeneratedRootContext.ToLeaf( " ).AppendTypeOf( handler.Owner.ClassType ).Append( " )," ).NewLine()
                             .AppendSourceString( handler.Method.Name ).Append( "," ).NewLine()
                             .AppendArray( handler.Parameters.Select( p => p.ParameterType ) ).Append( "," )
                             .Append( handler.Kind ).Append( "," )
                             .AppendSourceString( handler.FileName ).Append( "," )
                             .Append( handler.LineNumber )
                             .Append( ")," ).NewLine();
                        }
                        f.Append( "};" ).NewLine();

                        f.Append( "public bool IsHandled => true;" ).NewLine()
                         .Append( "public IReadOnlyList<CK.Cris.ICrisPocoModel.IHandler> Handlers => _handlers;" );
                    }
                    f.NewLine();
                }

                // The CrisPocoModel is the _factory field.
                var p = scope.Namespace.FindOrCreateAutoImplementedClass( monitor, e.CrisPocoInfo.PocoClass );
                p.GeneratedByComment().NewLine()
                 .Append( "public CK.Cris.ICrisPocoModel CrisPocoModel => _factory;" ).NewLine();

                scope.Append( p.FullName ).Append( "._factory,").NewLine();
            }
            scope.Append( "};" ).NewLine()
                 .Append( "return list;" )
                 .CloseBlock();

            // Publish the CommandRegistry in the services so that other can use it.
            c.CurrentRun.ServiceContainer.Add( _registry );
            return new CSCodeGenerationResult( nameof( CheckICommandHandlerImplementation ) );
        }

        CSCodeGenerationResult CheckICommandHandlerImplementation( IActivityMonitor monitor )
        {
            Throw.DebugAssert( _registry != null );

            CSCodeGenerationResult r = CSCodeGenerationResult.Success;
            var missingHandlers = _registry.CrisPocoModels.Where( c => c.CommandHandler == null );
            foreach( var c in missingHandlers )
            {
                if( c.ExpectedHandlerService != null )
                {
                    if( c.CrisPocoInfo.ClosureInterface != null )
                    {
                        monitor.Error( $"Service '{c.ExpectedHandlerService.ClassType.FullName}' must implement a command handler method for closed command {c.PocoName} of the closing type {c.CrisPocoInfo.ClosureInterface.FullName}." );
                    }
                    else
                    {
                        monitor.Error( $"Service '{c.ExpectedHandlerService.ClassType.FullName}' must implement a command handler method for unclosed command {c.PocoName} of primary type {c.CrisPocoInfo.PrimaryInterface.FullName}." );
                    }
                    r = CSCodeGenerationResult.Failed;
                }
                else
                {
                    monitor.Warn( $"Command {c.PocoName} for primary type {c.CrisPocoInfo.PrimaryInterface.FullName} has no associated handler." );
                }
            }
            return r;
        }
    }
}
