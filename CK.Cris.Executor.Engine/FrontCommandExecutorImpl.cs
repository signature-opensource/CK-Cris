using CK.CodeGen;
using CK.CodeGen.Abstractions;
using CK.Core;
using CK.Cris;
using CK.Text;
using System;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup.Cris
{
    public partial class FrontCommandExecutorImpl : AutoImplementorType
    {

        public override AutoImplementationResult Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
        {
            if( classType != typeof( FrontCommandExecutor ) ) throw new InvalidOperationException( "Applies only to the FrontCommandExecutor class." );
            var registry = CommandRegistry.FindOrCreate( monitor, c );
            if( registry == null ) return AutoImplementationResult.Failed;

            Debug.Assert( nameof( FrontCommandExecutor.ExecuteCommandAsync ) == "ExecuteCommandAsync" );
            Debug.Assert( classType.GetMethod( nameof( FrontCommandExecutor.ExecuteCommandAsync ), new[] { typeof( IActivityMonitor ), typeof( IServiceProvider ), typeof( KnownCommand ) } ) != null );

            var mExecute = scope.CreateFunction( "protected override Task<object> DoExecuteCommandAsync( IActivityMonitor m, IServiceProvider s, CK.Cris.KnownCommand c )" );
            mExecute.Append( "return _handlers[c.Model.CommandIdx]( m, s, c );" );

            const string funcSignature = "Func<IActivityMonitor, IServiceProvider, CK.Cris.KnownCommand, Task<object>>";
            foreach( var e in registry.Commands )
            {
                var h = e.Handler;
                if( h != null )
                {
                    Debug.Assert( h.UnwrappedReturnType != typeof( NoWaitResult ), "This has been checked before: NoWaitResult expects a void return." );
                    bool isVoidReturn = h.UnwrappedReturnType == typeof( void );
                    bool isHandlerAsync = h.IsRefAsync || h.IsValAsync;
                    bool isPostHandlerAsync = e.HasPostHandlerAsyncCall;

                    bool isOverallAsync = isHandlerAsync || isPostHandlerAsync;

                    scope.Append( "static " );
                    if( isOverallAsync ) scope.Append( "async " );
                    scope.Append( "Task<object> H" ).Append( e.CommandIdx ).Append( "( IActivityMonitor m, IServiceProvider s, CK.Cris.KnownCommand c )" ).NewLine()
                         .Append( "{" ).NewLine();
                    scope.Append( "var handler = (" ).AppendCSharpName( h.Method.DeclaringType! ).Append( ")s.GetService(" ).AppendTypeOf( h.Method.DeclaringType! ).Append( ");" ).NewLine();

                    if( !isVoidReturn ) scope.AppendCSharpName( e.ResultType ).Append( " r = " );
                    if( isHandlerAsync ) scope.Append( "await " );
                    scope.Append( "handler." ).Append( h.Method.Name ).Append( "( " );
                    foreach( var p in h.Parameters )
                    {
                        if( p.Position > 0 ) scope.Append( ", " );
                        if( typeof( IActivityMonitor ).IsAssignableFrom( p.ParameterType ) )
                        {
                            scope.Append( "m" );
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

                    e.GeneratePostHandlerCallCode( scope );

                    if( isVoidReturn )
                    {
                        if( isOverallAsync )
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
                        if( isOverallAsync )
                        {
                            scope.Append( "return (object)r" );
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

            scope.Append( "static readonly " ).Append( funcSignature ).Append( " NoHandler = ( m, s, c ) => throw new Exception( \"No Command handler found.\" );" ).NewLine();

            scope.Append( "readonly " ).Append( funcSignature ).Append( "[] _handlers = new " ).Append( funcSignature ).Append( "[" ).Append( registry.Commands.Count ).Append( "]{" );
            foreach( var e in registry.Commands )
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
