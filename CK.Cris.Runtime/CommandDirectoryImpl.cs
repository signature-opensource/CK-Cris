using CK.CodeGen;
using CK.CodeGen.Abstractions;
using CK.Core;
using CK.Cris;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

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

            public CommandModel( Type t, int i, string n, string[] p, Type r, Func<CK.Cris.ICommand> f, bool h )
            {
                CommandType = t;
                CommandIdx = i;
                CommandName = n;
                PreviousNames = p;
                ResultType = r;
                HasHandler = h;
                _f = f;
            }

            public Type CommandType { get; }

            public int CommandIdx { get; }

            public string CommandName { get; }

            public IReadOnlyList<string> PreviousNames { get; }

            public Type ResultType { get; }

            public bool HasHandler { get; }

            public CK.Cris.ICommand CreateInstance() => _f();
        }";

        public override AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
        {
            if( classType != typeof( CommandDirectory ) ) throw new InvalidOperationException( "Applies only to the CommandDirectory class." );
            var commands = CommandRegistry.FindOrCreate( monitor, c );
            if( commands == null ) return AutoImplementationResult.Failed;

            CodeWriterExtensions.Append( scope, CommandModel ).NewLine();
            CodeWriterExtensions.Append( scope, "public " ).Append( scope.Name ).Append( "() : base( CreateData() ) {}" ).NewLine();

            scope.Append( "static (IReadOnlyList<CK.Cris.ICommandModel> commands, IReadOnlyDictionary<object,CK.Cris.ICommandModel> index) CreateData()" ).NewLine()
                 .Append( "{" ).NewLine()
                 .Append( "CommandModel m;" ).NewLine()
                    // The IndexedCommands maps the names and the IPocoRootInfo: its the same number of entries as our 'map' target since
                    // the final PocoClass type replaces the IPocoRootInfo.
                 .Append( "var map = new Dictionary<object,CK.Cris.ICommandModel>(" ).Append( commands.IndexedCommands.Count ).Append( ");" ).NewLine()
                 .Append( "var list = new CommandModel[" ).Append( commands.Commands.Count ).Append( "];" ).NewLine();
            foreach( var e in commands.Commands )
            {
                scope.Append( "m = list[" ).Append( e.CommandIdx ).Append( "] = new CommandModel( " )
                                                                                .AppendTypeOf( e.Command.ClosureInterface ).Append( ", " )
                                                                                .Append( e.CommandIdx ).Append( ", " )
                                                                                .AppendSourceString( e.CommandName ).Append(", ")
                                                                                .AppendArray( e.PreviousNames ).Append( ", " )
                                                                                .AppendTypeOf( e.ResultType ).Append( ", " )
                                                                                .Append( "() => new " ).Append( e.Command.PocoClass.FullName ).Append( "()," )
                                                                                .Append( e.Handler != null )
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

            return AutoImplementationResult.Success;
        }
    }
}
