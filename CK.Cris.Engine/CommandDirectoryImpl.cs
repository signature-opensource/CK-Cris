using CK.CodeGen;
using CK.Core;
using CK.Cris;
using System;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Code generator of the <see cref="CommandDirectory"/> service.
    /// </summary>
    public class CommandDirectoryImpl : CSCodeGeneratorType
    {
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            // We need the IJsonSerializationCodeGen service to register command result type.
            return new CSCodeGenerationResult( nameof( DoImplement ) );
        }

        CSCodeGenerationResult DoImplement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope, IJsonSerializationCodeGen? json = null )
        { 
            if( classType != typeof( CommandDirectory ) ) throw new InvalidOperationException( "Applies only to the CommandDirectory class." );
            var registry = CommandRegistry.FindOrCreate( monitor, c );
            if( registry == null ) return CSCodeGenerationResult.Failed;

            CodeWriterExtensions.Append( scope, "public " ).Append( scope.Name ).Append( "() : base( CreateCommands() ) {}" ).NewLine();

            scope.Append( "static IReadOnlyList<CK.Cris.ICommandModel> CreateCommands()" ).NewLine()
                 .OpenBlock()
                 .Append( "var list = new ICommandModel[]" ).NewLine()
                 .Append( "{" ).NewLine();
            foreach( var e in registry.Commands )
            {
                if( json != null && e.ResultType != typeof(void) )
                {
                    json.RegisterEnumOrCollectionType( e.ResultType );
                }
                var f = c.Assembly.FindOrCreateAutoImplementedClass( monitor, e.Command.PocoFactoryClass );
                f.Definition.BaseTypes.Add( new ExtendedTypeName( "CK.Cris.ICommandModel" ) );
                f.Append( "public Type CommandType => PocoClassType;" ).NewLine()
                 .Append( "public int CommandIdx => " ).Append( e.CommandIdx ).Append( ";" ).NewLine()
                 .Append( "public string CommandName => Name;" ).NewLine()
                 .Append( "public Type ResultType => " ).AppendTypeOf( e.ResultType ).Append( ";" ).NewLine()
                 .Append( "public MethodInfo Handler => " ).Append( e.Handler?.Method ).Append( ";" ).NewLine()
                 .Append( "CK.Cris.ICommand CK.Cris.ICommandModel.Create() => (CK.Cris.ICommand)Create();" ).NewLine();

                var p = c.Assembly.FindOrCreateAutoImplementedClass( monitor, e.Command.PocoClass );
                p.Append( "public CK.Cris.ICommandModel CommandModel => _factory;" ).NewLine();

                scope.Append( p.FullName ).Append( "._factory,").NewLine();
            }
            scope.Append( "};" ).NewLine()
                 .Append( "return list;" )
                 .CloseBlock();

            c.CurrentRun.ServiceContainer.Add( registry );
            return new CSCodeGenerationResult( "CheckICommandHandlerImplementation" );
        }

        CSCodeGenerationResult CheckICommandHandlerImplementation( IActivityMonitor monitor, CommandRegistry registry )
        {
            CSCodeGenerationResult r = CSCodeGenerationResult.Success;

            var missingHandlers = registry.Commands.Where( c => c.Handler == null );
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
