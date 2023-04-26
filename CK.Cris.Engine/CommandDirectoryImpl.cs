using CK.CodeGen;
using CK.Core;
using CK.Cris;
using CK.Setup.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Code generator of the <see cref="CommandDirectory"/> service.
    /// </summary>
    public partial class CommandDirectoryImpl : CSCodeGeneratorType
    {
        // We keep a reference instead of using CommandRegistry.FindOrCreate each time (for TypeScript).
        CommandRegistry? _registry;

        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            // We need the JsonSerializationCodeGen service to register command result type.
            return new CSCodeGenerationResult( nameof( DoImplement ) );
        }

        CSCodeGenerationResult DoImplement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope, JsonSerializationCodeGen? json = null )
        { 
            Throw.CheckState( "Applies only to the CommandDirectory class.", classType == typeof( CommandDirectory ) );
            _registry = CommandRegistry.FindOrCreate( monitor, c );
            if( _registry == null ) return CSCodeGenerationResult.Failed;

            scope.Workspace.Global.FindOrCreateNamespace( "CK" ).Append( @"
                sealed class CRISCommandHandlerDesc : CK.Cris.ICommandModel.IHandler
                {
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

                    public CRISCommandHandlerDesc( TEMPORARYStObjFinalClass type,
                                                   string methodName,
                                                   Type[] parameters )
                    {
                        Type = type;
                        MethodName = methodName;
                        Parameters = parameters;
                    }

                    public IStObjFinalClass Type { get; }

                    public string MethodName { get; }

                    public Type[] Parameters { get; }
                }
" );

            scope.GeneratedByComment().NewLine()
                 .Append( "public " ).Append( scope.Name ).Append( "() : base( CreateCommands() ) {}" ).NewLine();

            scope.Append( "static IReadOnlyList<CK.Cris.ICommandModel> CreateCommands()" ).NewLine()
                 .OpenBlock()
                 .Append( "var list = new CK.Cris.ICommandModel[]" ).NewLine()
                 .Append( "{" ).NewLine();
            foreach( var e in _registry.Commands )
            {
                // Registering non Poco result type (Poco are all registered by JsonSerializationCodeGen).
                if( json != null
                    && e.ResultType != typeof(void)
                    && e.PocoResultType != null
                    && !json.IsAllowedType( e.ResultType ) )
                {
                    if( !json.AllowType( e.ResultNullableTypeTree ) )
                    {
                        monitor.Error( $"Failed to allow returned type '{e.ResultNullableTypeTree}' in JSON for command '{e.CommandName}'." );
                        return CSCodeGenerationResult.Failed;
                    }
                }
                var f = c.Assembly.FindOrCreateAutoImplementedClass( monitor, e.Command.PocoFactoryClass );
                f.Definition.BaseTypes.Add( new ExtendedTypeName( "CK.Cris.ICommandModel" ) );
                f.Append( "public Type CommandType => PocoClassType;" ).NewLine()
                 .Append( "public int CommandIdx => " ).Append( e.CommandIdx ).Append( ";" ).NewLine()
                 .Append( "public string CommandName => Name;" ).NewLine()
                 .Append( "public Type ResultType => " ).AppendTypeOf( e.ResultType ).Append( ";" ).NewLine()
                 .Append( "CK.Cris.ICommand CK.Cris.ICommandModel.Create() => (CK.Cris.ICommand)Create();" ).NewLine();

                if( e.Handler == null )
                {
                    f.Append( "public CK.Cris.ICommandModel.IHandler? Handler => null;" );
                }
                else
                {
                    f.Append( "static readonly CK.CRISCommandHandlerDesc.TEMPORARYStObjFinalClass _tempFinalClass = new CK.CRISCommandHandlerDesc.TEMPORARYStObjFinalClass(" ).NewLine()
                        .AppendTypeOf( e.Handler.Owner.ClassType ).Append( ", " ).NewLine()
                        .AppendTypeOf( e.Handler.Owner.FinalType ).Append( ", " ).NewLine()
                        .Append( e.Handler.Owner.IsScoped ).Append( ", " ).NewLine()
                        .AppendArray( e.Handler.Owner.MultipleMappings ).Append( ", " ).NewLine()
                        .AppendArray( e.Handler.Owner.UniqueMappings ).Append( " );" ).NewLine();

                    f.Append( "static readonly CK.Cris.ICommandModel.IHandler _cmdHandlerDesc = new CK.CRISCommandHandlerDesc(" ).NewLine()
                     .Append( "_tempFinalClass," ).NewLine()
                     .AppendSourceString( e.Handler.Method.Name ).Append(",").NewLine()
                     .AppendArray( e.Handler.Parameters.Select( p => p.ParameterType )).Append(");").NewLine();

                    f.Append( "public CK.Cris.ICommandModel.IHandler? Handler => _cmdHandlerDesc;" );
                }
                f.NewLine();

                // The CommandModel is the _factory field.
                var p = c.Assembly.FindOrCreateAutoImplementedClass( monitor, e.Command.PocoClass );
                p.Append( "public CK.Cris.ICommandModel CommandModel => _factory;" ).NewLine();

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
            var missingHandlers = _registry.Commands.Where( c => c.Handler == null );
            foreach( var c in missingHandlers )
            {
                if( c.ExpectedHandlerService != null )
                {
                    if( c.Command.ClosureInterface != null )
                    {
                        monitor.Error( $"Service '{c.ExpectedHandlerService.ClassType.FullName}' must implement a command handler method for closed command {c.CommandName} of the closing type {c.Command.ClosureInterface.FullName}." );
                    }
                    else
                    {
                        monitor.Error( $"Service '{c.ExpectedHandlerService.ClassType.FullName}' must implement a command handler method for unclosed command {c.CommandName} of primary type {c.Command.PrimaryInterface.FullName}." );
                    }
                    r = CSCodeGenerationResult.Failed;
                }
                else
                {
                    monitor.Warn( $"Command {c.CommandName} for primary type {c.Command.PrimaryInterface.FullName} has no associated handler." );
                }
            }
            return r;
        }

    }
}
