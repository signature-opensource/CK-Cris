using CK.CodeGen;
using CK.Core;
using CK.Cris;
using System;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup.Cris
{
    public partial class FrontCommandExecutorImpl : CSCodeGeneratorType
    {

        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            Throw.CheckState( "Applies only to the FrontCommandExecutor class.", classType ==typeof( FrontCommandExecutor ) );
            var registry = CommandRegistry.FindOrCreate( monitor, c );
            if( registry == null ) return CSCodeGenerationResult.Failed;

            Debug.Assert( nameof( FrontCommandExecutor.ExecuteCommandAsync ) == "ExecuteCommandAsync" );
            Debug.Assert( classType.GetMethod( nameof( FrontCommandExecutor.ExecuteCommandAsync ), new[] { typeof( IActivityMonitor ), typeof( IServiceProvider ), typeof( ICommand ) } ) != null );

            var mExecute = scope.CreateFunction( "protected override Task<object> DoExecuteCommandAsync( IActivityMonitor m, IServiceProvider s, CK.Cris.ICommand c )" );
            mExecute.Append( "return _handlers[c.CommandModel.CommandIdx]( m, s, c );" );

            const string funcSignature = "Func<IActivityMonitor, IServiceProvider, CK.Cris.ICommand, Task<object>>";
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
                    scope.Append( "Task<object> H" ).Append( e.CommandIdx ).Append( "( IActivityMonitor m, IServiceProvider s, CK.Cris.ICommand c )" ).NewLine()
                         .Append( "{" ).NewLine();
                    scope.Append( "var handler = (" ).Append( h.Owner.FinalType.ToCSharpName() ).Append( ")s.GetService(" ).AppendTypeOf( h.Owner.FinalType ).Append( ");" ).NewLine();

                    if( !isVoidReturn ) scope.Append( e.ResultType.ToCSharpName() ).Append( " r = " );
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
                            scope.Append( "(" ).Append( h.CommandParameter.ParameterType.ToCSharpName() ).Append( ")c" );
                        }
                        else
                        {
                            scope.Append( "(" ).Append( p.ParameterType.ToCSharpName() ).Append( ")s.GetService(" ).AppendTypeOf( p.ParameterType ).Append( ")" );
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

            return CSCodeGenerationResult.Success;
        }

    }

}
