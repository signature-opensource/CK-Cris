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

        // Auto registers IAmbientValues so that tests don't have to register it explicitly: registering CrisDirectory is enough.
        void IAttributeContextBoundInitializer.Initialize( IActivityMonitor monitor, ITypeAttributesCache owner, MemberInfo m, Action<Type> alsoRegister )
        {
            alsoRegister( typeof( CK.Cris.AmbientValues.IAmbientValues ) );
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

            var handlerDescTypeScope = scope.Workspace.Global.FindOrCreateNamespace( "CK.Cris" ).CreateType( "sealed class CrisHandlerDescriptor : CK.Cris.ICrisPocoModel.IHandler" );

            handlerDescTypeScope.Append( @"
    // Waiting for ""StObj goes static"": the static fields of
    // GeneratedStObjContextRoot will expose the
    // static GFinalStObj[] _finalStObjs and services list so that the ""real""
    // GFinalStObj xor StObjServiceClassFactoryInfo can be used.
    public sealed class TEMPORARYStObjFinalClass : IStObjFinalClass
    {
        public TEMPORARYStObjFinalClass( Type classType,
                                         Type finalType,
                                         bool isScoped,
                                         IReadOnlyCollection<Type> multipleMappings,
                                         IReadOnlyCollection<Type> uniqueMappings )
        {
            ClassType = classType;
            FinalType = finalType;
            IsScoped = isScoped;
            MultipleMappings = multipleMappings;
            UniqueMappings = uniqueMappings;
        }
        public Type ClassType { get; }

        public Type FinalType { get; }

        public bool IsScoped { get; }

        public IReadOnlyCollection<Type> MultipleMappings { get; }

        public IReadOnlyCollection<Type> UniqueMappings { get; }
    }

    public CrisHandlerDescriptor( TEMPORARYStObjFinalClass type,
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
" );

            scope.GeneratedByComment().NewLine()
                 .Append( "public " ).Append( scope.Name ).Append( "() : base( CreateCommands() ) {}" ).NewLine();

            // Temporary. Waiting for "StObj goes Static".
            Dictionary<IStObjFinalClass,string> stObjStatic = new Dictionary<IStObjFinalClass,string>();

            static string GetStObjFinalStatic( ITypeScope f, IStObjFinalClass c, Dictionary<IStObjFinalClass, string> stObjStatic )
            {
                if( !stObjStatic.TryGetValue( c, out var result ) )
                {
                    result = $"_tempStObj{stObjStatic.Count}";
                    f.Append( "public static readonly TEMPORARYStObjFinalClass " ).Append( result ).Append( " = new TEMPORARYStObjFinalClass(" ).NewLine()
                     .AppendTypeOf( c.ClassType ).Append( ", " ).NewLine()
                     .AppendTypeOf( c.FinalType ).Append( ", " ).NewLine()
                     .Append( c.IsScoped ).Append( ", " ).NewLine()
                     .AppendArray( c.MultipleMappings ).Append( ", " ).NewLine()
                     .AppendArray( c.UniqueMappings ).Append( " );" ).NewLine();
                    stObjStatic.Add( c, result );
                }
                return $"{f.FullName}.{result}";
            }

            scope.Append( "static IReadOnlyList<CK.Cris.ICrisPocoModel> CreateCommands()" )
                 .OpenBlock()
                 .Append( "var list = new CK.Cris.ICrisPocoModel[]" ).NewLine()
                 .Append( "{" ).NewLine();
            foreach( var e in _registry.CrisPocoModels )
            {
                // Registering non Poco result type (Poco are all registered by JsonSerializationCodeGen).
                if( json != null
                    && e.ResultType != typeof(void)
                    && e.PocoResultType != null
                    && !json.IsAllowedType( e.ResultType ) )
                {
                    if( !json.AllowType( e.ResultNullableTypeTree ) )
                    {
                        monitor.Error( $"Failed to allow returned type '{e.ResultNullableTypeTree}' in JSON for command '{e.PocoName}'." );
                        return CSCodeGenerationResult.Failed;
                    }
                }
                var f = c.Assembly.FindOrCreateAutoImplementedClass( monitor, e.CrisPocoInfo.PocoFactoryClass );
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
                    var allHandlers = e.Handler != null
                                        ? ((IEnumerable<CrisRegistry.BaseHandler>)e.Validators).Append( e.Handler ).Concat( e.PostHandlers )
                                        : e.EventHandlers;
                    Debug.Assert( allHandlers.Any() );

                    f.Append( "static readonly CK.Cris.ICrisPocoModel.IHandler[] _handlers = new [] {" ).NewLine();
                    foreach( var handler in allHandlers )
                    {
                        f.Append( "new CK.Cris.CrisHandlerDescriptor(" ).NewLine()
                         .Append( GetStObjFinalStatic( handlerDescTypeScope, handler.Owner, stObjStatic ) ).Append( "," ).NewLine()
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

                // The CrisPocoModel is the _factory field.
                var p = c.Assembly.FindOrCreateAutoImplementedClass( monitor, e.CrisPocoInfo.PocoClass );
                p.Append( "public CK.Cris.ICrisPocoModel CrisPocoModel => _factory;" ).NewLine();

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
            Debug.Assert( _registry != null );

            CSCodeGenerationResult r = CSCodeGenerationResult.Success;
            var missingHandlers = _registry.CrisPocoModels.Where( c => c.Handler == null );
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
