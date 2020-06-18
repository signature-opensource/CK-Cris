using CK.CodeGen;
using CK.CodeGen.Abstractions;
using CK.Core;
using CK.Cris;
using CK.Text;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CK.Setup.Cris
{
    public partial class FrontCommandExecutorImpl : AutoImplementorType
    {

        public override AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
        {
            if( classType != typeof( FrontCommandExecutor ) ) throw new InvalidOperationException( "Applies only to the FrontCommandReceiver class." );
            var commands = CommandRegistry.FindOrCreate( monitor, c );
            if( commands == null ) return AutoImplementationResult.Failed;

            Debug.Assert( nameof( FrontCommandExecutor.ExecuteCommandAsync ) == "ExecuteCommandAsync" );
            Debug.Assert( classType.GetMethod( nameof( FrontCommandExecutor.ExecuteCommandAsync ), new[] { typeof( IActivityMonitor ), typeof( IServiceProvider ), typeof( KnownCommand ), typeof( CommandCallerInfo ) } ) != null );

            var mExecute = scope.CreateFunction( "protected override Task<object> DoExecuteCommandAsync( IActivityMonitor m, IServiceProvider s, CK.Cris.KnownCommand c, CK.Cris.CommandCallerInfo i )" );
            mExecute.Append( "return _handlers[c.Model.CommandIdx]( m, s, c, i );" );

            const string funcSignature = "Func<IActivityMonitor, IServiceProvider, CK.Cris.KnownCommand, CK.Cris.CommandCallerInfo, Task<object>>";
            foreach( var e in commands.Commands )
            {
                var h = e.Handler;
                if( h != null )
                {
                    bool isVoidReturn = h.UnwrappedReturnType == typeof( void );
                    bool isAsync = h.IsRefAsync || h.IsValAsync;

                    scope.Append( "static " );
                    if( isAsync ) scope.Append( "async " );
                    scope.Append( "Task<object> H" ).Append( e.CommandIdx ).Append( "( IActivityMonitor m, IServiceProvider s, CK.Cris.KnownCommand c, CK.Cris.CommandCallerInfo i )" ).NewLine()
                         .Append( "{" ).NewLine();
                    scope.Append( "var h = (" ).AppendCSharpName( h.Owner.ClassType ).Append( ")s.GetService(" ).AppendTypeOf( h.Owner.ClassType ).Append( ");" ).NewLine();


                    if( !isVoidReturn ) scope.Append( "object r = " );
                    if( isAsync ) scope.Append( "await " );
                    scope.Append( "h." ).Append( h.Method.Name ).Append( "( " );
                    foreach( var p in h.Parameters )
                    {
                        if( p.Position > 0 ) scope.Append( ", " );
                        if( typeof( IActivityMonitor ).IsAssignableFrom( p.ParameterType ) )
                        {
                            scope.Append( "m" );
                        }
                        else if( p.ParameterType == typeof( CommandCallerInfo ) )
                        {
                            scope.Append( "i" );
                        }
                        else if( p == h.CommandParameter )
                        {
                            scope.Append( "(" ).AppendCSharpName( h.CommandParameter.ParameterType ).Append( ")c.Command" );
                        }
                        else
                        {
                            scope.Append( "(" ).AppendCSharpName( p.ParameterType ).Append( ")s.GetService(" ).AppendTypeOf( p.ParameterType ).Append( ")" );
                        }
                    }
                    scope.Append( " );" ).NewLine();

                    if( isVoidReturn )
                    {
                        if( isAsync )
                        {
                            scope.Append( "return null" );
                        }
                        else
                        {
                            scope.Append( "return Task.FromResult<object>( null )" );
                        }
                    }
                    else 
                    {
                        if( isAsync )
                        {
                            scope.Append( "return r" );
                        }
                        else
                        {
                            scope.Append( "return Task.FromResult( (object)r )" );
                        }
                    }
                    scope.Append( ";" ).NewLine()
                         .Append( "}" ).NewLine();
                }
            }

            scope.Append( "static readonly " ).Append( funcSignature ).Append( " NoHandler = ( m, s, c, i ) => throw new Exception( \"No Command handler found.\" );" ).NewLine();

            scope.Append( "readonly " ).Append( funcSignature ).Append( "[] _handlers = new " ).Append( funcSignature ).Append( "[" ).Append( commands.Commands.Count ).Append( "]{" );
            foreach( var e in commands.Commands )
            {
                if( e.CommandIdx != 0 ) scope.Append( ", " );
                if( e.Handler == null )
                {
                    scope.Append( "NoHandler" );
                }
                else
                {
                    scope.Append( "H" ).Append( e.CommandIdx );
                }
            }
            scope.Append( "};" )
                 .NewLine();

            return AutoImplementationResult.Success;
        }

    }

}
