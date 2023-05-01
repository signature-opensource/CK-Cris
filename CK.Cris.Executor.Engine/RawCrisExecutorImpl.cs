using CK.CodeGen;
using CK.Core;
using CK.Cris;
using System;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup.Cris
{

    public partial class RawCrisExecutorImpl : CSCodeGeneratorType
    {
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            Throw.CheckState( "Applies only to the RawCrisExecutor class.", classType == typeof( RawCrisExecutor ) );
            var registry = CommandRegistry.FindOrCreate( monitor, c );
            if( registry == null ) return CSCodeGenerationResult.Failed;

            Debug.Assert( nameof( RawCrisExecutor.RawExecuteAsync ) == "RawExecuteAsync" );
            Debug.Assert( classType.GetMethod( nameof( RawCrisExecutor.RawExecuteAsync ), new[] { typeof( IServiceProvider ), typeof( ICrisPoco ) } ) != null );

            var mExecute = scope.CreateFunction( "public override Task<object> RawExecuteAsync( IServiceProvider s, CK.Cris.ICrisPoco c )" );
            mExecute.GeneratedByComment().NewLine()
                    .Append( "return _handlers[c.CrisPocoModel.CrisPocoIndex]( s, c );" );

            const string funcSignature = "Func<IServiceProvider, CK.Cris.ICrisPoco, Task<object>>";
            foreach( var e in registry.CrisPocoModels )
            {
                var h = e.Handler;
                if( h != null )
                {
                    bool isVoidReturn = h.UnwrappedReturnType == typeof( void );
                    bool isHandlerAsync = h.IsRefAsync || h.IsValAsync;
                    bool isPostHandlerAsync = e.HasPostHandlerAsyncCall;

                    bool isOverallAsync = isHandlerAsync || isPostHandlerAsync;

                    scope.Append( "static " );
                    if( isOverallAsync ) scope.Append( "async " );
                    scope.Append( "Task<object> H" ).Append( e.CrisPocoIndex ).Append( "( IServiceProvider s, CK.Cris.ICrisPoco c )" ).NewLine()
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
            scope.Append( "readonly " ).Append( funcSignature ).Append( "[] _handlers = new " ).Append( funcSignature ).Append( "[" ).Append( registry.CrisPocoModels.Count ).Append( "]{" );
            foreach( var e in registry.CrisPocoModels )
            {
                if( e.CrisPocoIndex != 0 ) scope.Append( ", " );
                if( e.Handler == null )
                {
                    scope.Append( "NoHandler" );
                    needNoHandler = true;
                }
                else
                {
                    scope.Append( "H" ).Append( e.CrisPocoIndex );
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
