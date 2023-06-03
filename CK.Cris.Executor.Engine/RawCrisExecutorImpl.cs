using CK.CodeGen;
using CK.Core;
using CK.Cris;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using static CK.Setup.Cris.CrisRegistry;
using static System.Formats.Asn1.AsnWriter;

namespace CK.Setup.Cris
{

    public partial class RawCrisExecutorImpl : CSCodeGeneratorType
    {
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            Throw.CheckState( "Applies only to the RawCrisExecutor class.", classType == typeof( RawCrisExecutor ) );
            var registry = CrisRegistry.FindOrCreate( monitor, c );
            if( registry == null ) return CSCodeGenerationResult.Failed;
            scope.Definition.Modifiers |= Modifiers.Sealed;

            using var scopeRegion = scope.Region();

            CreateExecutorMethods( classType, scope );

            // Creates the static handlers functions.
            foreach( var e in registry.CrisPocoModels )
            {
                var h = e.CommandHandler;
                if( h != null )
                {
                    CreateCommandHandler( scope, e, h );
                }
                else if( e.EventHandlers.Count > 0 )
                {
                    CreateEventHandler( scope, e );
                }
            }

            // To accommodate RoutedEventHandler void/Task returns, the array of handlers returns a Task instead of
            // Task<object>. The RawExecuteAsync method downcasts the command entries to Task<object>.
            // The DispatchEventAsync uses mere tasks. This enables an optimization for events with a single
            // asynchronous handler.
            const string funcSignature = "Func<IServiceProvider, CK.Cris.ICrisPoco, Task>";
            scope.Append( "readonly " ).Append( funcSignature ).Append( "[] _handlers = new " ).Append( funcSignature ).Append( "[]{" );
            bool needNoHandler = false;
            foreach( var e in registry.CrisPocoModels )
            {
                if( e.CrisPocoIndex != 0 ) scope.Append( ", " );
                if( e.IsHandled )
                {
                    scope.Append( "H" ).Append( e.CrisPocoIndex );
                }
                else
                {
                    scope.Append( "NoHandler" );
                    needNoHandler = true;
                }
            }
            scope.Append( "};" ).NewLine();
            if( needNoHandler )
            {
                scope.Append( "static readonly " ).Append( funcSignature ).Append( " NoHandler = ( s, c ) => Throw.CKException<Task<object>>( \"No Command handler found.\" );" ).NewLine();
            }

            return CSCodeGenerationResult.Success;
        }

        static void CreateEventHandler( ITypeScope scope, CrisRegistry.Entry e )
        {
            var func = scope.CreateFunction( $"static Task H{e.CrisPocoIndex}(IServiceProvider s, CK.Cris.ICrisPoco c)" );

            //func.GeneratedByComment()
            //     .Append("static ").CreatePart( out var asyncModifier ).Append( "Task H" ).Append( e.CrisPocoIndex )
            //     .Append( "( IServiceProvider s, CK.Cris.ICrisPoco c )" )
            //     .OpenBlock();

            var cachedServices = new VariableCachedServices( func.CreatePart() );

            func.GeneratedByComment();
            int syncHandlerCount = 0;
            foreach( var calls in e.EventHandlers.Where( h => !h.IsRefAsync && !h.IsValAsync ).GroupBy( h => h.Owner ) )
            {
                foreach( var h in calls )
                {
                    InlineCallOwnerMethod( func, h, cachedServices, h.EventOrPartParameter ).Append( ";" ).NewLine();
                    ++syncHandlerCount;
                }
            }
            Debug.Assert( syncHandlerCount <= e.EventHandlers.Count );
            // Are there async handlers?
            int asyncHandlerCount = e.EventHandlers.Count - syncHandlerCount;
            if( asyncHandlerCount > 0 )
            {
                if( asyncHandlerCount == 1 )
                {
                    var h = e.EventHandlers.Single( h => h.IsRefAsync || h.IsValAsync );
                    func.Append( "// No async state machine required." ).NewLine()
                         .Append( "return " );
                    InlineCallOwnerMethod( func, h, cachedServices, h.EventOrPartParameter )
                        .Append( h.IsValAsync ? ".AsTask();" : ";" );
                    return;
                }
                func.Definition.Modifiers |= Modifiers.Async;
                foreach( var calls in e.EventHandlers.Where( h => h.IsRefAsync || h.IsValAsync ).GroupBy( h => h.Owner ) )
                {
                    foreach( var h in calls )
                    {
                        func.Append( "await " );
                        InlineCallOwnerMethod( func, h, cachedServices, h.EventOrPartParameter ).Append( ";" ).NewLine();
                    }
                }
            }
            else
            {
                func.Append( "return Task.CompletedTask;" );
            }
        }

        static void CreateCommandHandler( ITypeScope scope, CrisRegistry.Entry e, CrisRegistry.HandlerMethod h )
        {
            bool isVoidReturn = h.UnwrappedReturnType == typeof( void );
            bool isHandlerAsync = h.IsRefAsync || h.IsValAsync;
            bool isPostHandlerAsync = e.HasPostHandlerAsyncCall;

            bool isOverallAsync = isHandlerAsync || isPostHandlerAsync;

            scope.GeneratedByComment().Append( "static " );
            if( isOverallAsync ) scope.Append( "async " );
            scope.Append( "Task<object> H" ).Append( e.CrisPocoIndex ).Append( "( IServiceProvider s, CK.Cris.ICrisPoco c )" )
                 .OpenBlock();

            var cachedServices = new VariableCachedServices( scope.CreatePart() );

            if( !isVoidReturn ) scope.AppendGlobalTypeName( e.ResultType ).Append( " r = " );
            if( isHandlerAsync ) scope.Append( "await " );
            InlineCallOwnerMethod( scope, h, cachedServices, h.CommandParameter ).Append(";").NewLine();

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
            scope.Append( ";" )
            .CloseBlock();
        }

        static ICodeWriter InlineCallOwnerMethod( ICodeWriter w, BaseHandler h, VariableCachedServices cachedServices, ParameterInfo crisPocoParameter )
        {
            if( h.Method.DeclaringType != h.Owner.ClassType )
            {
                w.Append( "((" ).AppendGlobalTypeName( h.Method.DeclaringType ).Append( ")" );
                cachedServices.WriteGetService( w, h.Owner.ClassType ).Append( ")" );
            }
            else
            {
                cachedServices.WriteGetService( w, h.Owner.ClassType );
            }
            w.Append( "." ).Append( h.Method.Name ).Append( "( " );
            foreach( var p in h.Parameters )
            {
                if( p.Position > 0 ) w.Append( ", " );
                if( p == crisPocoParameter )
                {
                    w.Append( "(" ).Append( crisPocoParameter.ParameterType.ToGlobalTypeName() ).Append( ")c" );
                }
                else
                {
                    cachedServices.WriteGetService( w, p.ParameterType );
                }
            }
            w.Append( " )" );
            return w;
        }

        static void CreateExecutorMethods( Type classType, ITypeScope scope )
        {
            Debug.Assert( nameof( RawCrisExecutor.RawExecuteAsync ) == "RawExecuteAsync" );
            Debug.Assert( classType.GetMethod( nameof( RawCrisExecutor.RawExecuteAsync ), new[] { typeof( IServiceProvider ), typeof( IAbstractCommand ) } ) != null );
            var mExecute = scope.CreateFunction( "public override Task<object> RawExecuteAsync( IServiceProvider s, CK.Cris.IAbstractCommand c )" );
            mExecute.GeneratedByComment().NewLine()
                    .Append( "return global::System.Runtime.CompilerServices.Unsafe.As<Task<object>>(_handlers[c.CrisPocoModel.CrisPocoIndex]( s, c ));" );

            Debug.Assert( nameof( RawCrisExecutor.DispatchEventAsync ) == "DispatchEventAsync" );
            Debug.Assert( classType.GetMethod( nameof( RawCrisExecutor.DispatchEventAsync ), new[] { typeof( IServiceProvider ), typeof( IEvent ) } ) != null );
            var mDispatchEvent = scope.CreateFunction( "public override Task DispatchEventAsync( IServiceProvider s, CK.Cris.IEvent e )" );
            mDispatchEvent.GeneratedByComment().NewLine()
                          .Append( "return _handlers[e.CrisPocoModel.CrisPocoIndex]( s, e );" );

            Debug.Assert( nameof( RawCrisExecutor.SafeDispatchEventAsync ) == "SafeDispatchEventAsync" );
            Debug.Assert( classType.GetMethod( nameof( RawCrisExecutor.SafeDispatchEventAsync ), new[] { typeof( IServiceProvider ), typeof( IEvent ) } ) != null );
            var mSafeDispatchEvent = scope.CreateFunction( "public override async Task<bool> SafeDispatchEventAsync( IServiceProvider s, CK.Cris.IEvent e )" );
            mSafeDispatchEvent.GeneratedByComment().NewLine()
                    .Append( """
                             try
                             {
                                await _handlers[e.CrisPocoModel.CrisPocoIndex]( s, e );
                                return true;
                             }
                             catch( Exception ex )
                             {
                                var monitor = (IActivityMonitor)s.GetService( typeof(IActivityMonitor) );
                                Throw.CheckState( monitor != null );
                                using( monitor.OpenError( $"Event '{e.CrisPocoModel.PocoName}' dispatch failed.", ex ) )
                                {
                                   monitor.Trace( e.ToString() );
                                }
                                return false;
                             }
                             """ );
        }
    }

}
