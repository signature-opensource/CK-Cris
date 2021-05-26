using CK.CodeGen;
using CK.Core;
using CK.Cris;
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
            // We need the IJsonSerializationCodeGen service to register command result type.
            return new CSCodeGenerationResult( nameof( DoImplement ) );
        }

        CSCodeGenerationResult DoImplement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope, IJsonSerializationCodeGen? json = null )
        { 
            if( classType != typeof( CommandDirectory ) ) throw new InvalidOperationException( "Applies only to the CommandDirectory class." );
            _registry = CommandRegistry.FindOrCreate( monitor, c );
            if( _registry == null ) return CSCodeGenerationResult.Failed;

            CodeWriterExtensions.Append( scope, "public " ).Append( scope.Name ).Append( "() : base( CreateCommands() ) {}" ).NewLine();

            scope.Append( "static IReadOnlyList<CK.Cris.ICommandModel> CreateCommands()" ).NewLine()
                 .OpenBlock()
                 .Append( "var list = new ICommandModel[]" ).NewLine()
                 .Append( "{" ).NewLine();
            foreach( var e in _registry.Commands )
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
