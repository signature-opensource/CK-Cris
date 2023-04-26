using CK.CodeGen;
using CK.Core;
using CK.Cris;
using System;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup.Cris
{

    public partial class RawCommandExecutorImpl : CSCodeGeneratorType
    {
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            Throw.CheckState( "Applies only to the RawCommandExecutor class.", classType == typeof( RawCommandExecutor ) );
            var registry = CommandRegistry.FindOrCreate( monitor, c );
            if( registry == null ) return CSCodeGenerationResult.Failed;

            Debug.Assert( nameof( RawCommandExecutor.RawExecuteCommandAsync ) == "RawExecuteCommandAsync" );
            Debug.Assert( classType.GetMethod( nameof( RawCommandExecutor.RawExecuteCommandAsync ), new[] { typeof( IServiceProvider ), typeof( ICommand ) } ) != null );

            var mExecute = scope.CreateFunction( "public override Task<object> RawExecuteCommandAsync( IServiceProvider s, CK.Cris.ICommand c )" );
            mExecute.GeneratedByComment().NewLine()
                    .Append( "return _handlers[c.CommandModel.CommandIdx]( s, c );" );

            const string funcSignature = "Func<IServiceProvider, CK.Cris.ICommand, Task<object>>";
            foreach( var e in registry.Commands )
            {
                var h = e.Handler;
                if( h != null )
                {
                    Debug.Assert( h.UnwrappedReturnType != typeof( ICrisEvent.NoWaitResult ), "This has been checked before: NoWaitResult expects a void return." );
                    bool isVoidReturn = h.UnwrappedReturnType == typeof( void );
                    bool isHandlerAsync = h.IsRefAsync || h.IsValAsync;
                    bool isPostHandlerAsync = e.HasPostHandlerAsyncCall;

                    bool isOverallAsync = isHandlerAsync || isPostHandlerAsync;

                    scope.Append( "static " );
                    if( isOverallAsync ) scope.Append( "async " );
                    scope.Append( "Task<object> H" ).Append( e.CommandIdx ).Append( "( IServiceProvider s, CK.Cris.ICommand c )" ).NewLine()
                         .Append( "{" ).NewLine()
                         .GeneratedByComment().NewLine();

                    var cachedServices = new VariableCachedServices( scope.CreatePart() );

                    // This handles any potential explicit implementation.
                    // Explicit implementations are not really a good idea, but if there are, they are handled.
                    Debug.Assert( h.Method.DeclaringType != null );
                    var callerType = h.Method.DeclaringType.IsInterface
                                        ? h.Method.DeclaringType
                                        : h.Owner.FinalType;
                    scope.Append( "var handler = (" ).Append( callerType.ToCSharpName() ).Append( ")s.GetService(" ).AppendTypeOf( h.Owner.ClassType ).Append( ");" ).NewLine();

                    if( !isVoidReturn ) scope.Append( e.ResultType.ToCSharpName() ).Append( " r = " );
                    if( isHandlerAsync ) scope.Append( "await " );

                    scope.Append( "handler." ).Append( h.Method.Name ).Append( "( " );
                    foreach( var p in h.Parameters )
                    {
                        if( p.Position > 0 ) scope.Append( ", " );
                        if( p == h.CommandParameter )
                        {
                            scope.Append( "(" ).Append( h.CommandParameter.ParameterType.ToCSharpName() ).Append( ")c" );
                        }
                        else
                        {
                            cachedServices.WriteGetService( scope, p.ParameterType );
                        }
                    }
                    scope.Append( " );" ).NewLine();

                    e.GeneratePostHandlerCallCode( scope, cachedServices );

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

            bool needNoHandler = false;
            scope.Append( "readonly " ).Append( funcSignature ).Append( "[] _handlers = new " ).Append( funcSignature ).Append( "[" ).Append( registry.Commands.Count ).Append( "]{" );
            foreach( var e in registry.Commands )
            {
                if( e.CommandIdx != 0 ) scope.Append( ", " );
                if( e.Handler == null )
                {
                    scope.Append( "NoHandler" );
                    needNoHandler = true;
                }
                else
                {
                    scope.Append( "H" ).Append( e.CommandIdx );
                }
            }
            scope.Append( "};" )
                 .NewLine();
            if( needNoHandler )
            {
                scope.Append( "static readonly " ).Append( funcSignature ).Append( " NoHandler = ( s, c ) => Throw.CKException<Task<object>>( \"No Command handler found.\" );" ).NewLine();
            }

            return CSCodeGenerationResult.Success;
        }

    }

}
