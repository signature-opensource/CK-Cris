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
    public class CommandDirectoryImpl : AutoImplementorType
    {
        const string CommandModel = @"
        class CommandModel : CK.Cris.ICommandModel
        {
            readonly Func<CK.Cris.ICommand> _f;

            public CommandModel( Type t, int i, string n, string[] p, Type r, Func<CK.Cris.ICommand> f, MethodInfo h )
            {
                CommandType = t;
                CommandIdx = i;
                CommandName = n;
                PreviousNames = p;
                ResultType = r;
                Handler = h;
                _f = f;
            }

            public Type CommandType { get; }

            public int CommandIdx { get; }

            public string CommandName { get; }

            public IReadOnlyList<string> PreviousNames { get; }

            public Type ResultType { get; }

            public MethodInfo Handler { get; }

            public CK.Cris.ICommand CreateInstance() => _f();
        }";

        public override AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
        {
            if( classType != typeof( CommandDirectory ) ) throw new InvalidOperationException( "Applies only to the CommandDirectory class." );
            var registry = CommandRegistry.FindOrCreate( monitor, c );
            if( registry == null ) return AutoImplementationResult.Failed;

            CodeWriterExtensions.Append( scope, CommandModel ).NewLine();
            CodeWriterExtensions.Append( scope, "public " ).Append( scope.Name ).Append( "() : base( CreateData() ) {}" ).NewLine();

            scope.Append( "static (IReadOnlyList<CK.Cris.ICommandModel> commands, IReadOnlyDictionary<object,CK.Cris.ICommandModel> index) CreateData()" ).NewLine()
                 .Append( "{" ).NewLine()
                 .Append( "CommandModel m;" ).NewLine()
                    // The IndexedCommands maps the names and the IPocoRootInfo: its the same number of entries as our 'map' target since
                    // the final PocoClass type replaces the IPocoRootInfo.
                 .Append( "var map = new Dictionary<object,CK.Cris.ICommandModel>(" ).Append( registry.IndexedCommands.Count ).Append( ");" ).NewLine()
                 .Append( "var list = new CommandModel[" ).Append( registry.Commands.Count ).Append( "];" ).NewLine();
            foreach( var e in registry.Commands )
            {
                scope.Append( "m = list[" ).Append( e.CommandIdx ).Append( "] = new CommandModel( " )
                                                                                .AppendTypeOf( e.Command.PocoClass ).Append( ", " )
                                                                                .Append( e.CommandIdx ).Append( ", " )
                                                                                .AppendSourceString( e.CommandName ).Append(", ")
                                                                                .AppendArray( e.PreviousNames ).Append( ", " )
                                                                                .AppendTypeOf( e.ResultType ).Append( ", " )
                                                                                .Append( "() => new " ).AppendCSharpName( e.Command.PocoClass ).Append( "()," )
                                                                                .Append( e.Handler?.Method )
                                                                                .Append(" );" )
                                                                                .NewLine();
                foreach( var n in e.PreviousNames.Append( e.CommandName ) )
                {
                    scope.Append( "map.Add( " ).AppendSourceString( n ).Append( ", m );" ).NewLine();
                }
                scope.Append( "map.Add( " ).AppendTypeOf( e.Command.PocoClass ).Append( ", m );" ).NewLine();
            }
            scope.Append( "return (list,map);" ).NewLine();
            scope.Append( "}" ).NewLine();

            c.CurrentRun.ServiceContainer.Add( registry );
            return new AutoImplementationResult( "CheckICommandHandlerImplementation" );
        }

        AutoImplementationResult CheckICommandHandlerImplementation( IActivityMonitor monitor, CommandRegistry registry )
        {
            AutoImplementationResult r = AutoImplementationResult.Success;

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
                    r = AutoImplementationResult.Failed;
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
