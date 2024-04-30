using CK.CodeGen;
using CK.Core;
using CK.Cris;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;

namespace CK.Setup.Cris
{

    public partial class RawCrisExecutorImpl : CSCodeGeneratorType
    {
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            Throw.CheckState( "Applies only to the RawCrisExecutor class.", classType == typeof( RawCrisExecutor ) );

            var crisEngineService = c.CurrentRun.ServiceContainer.GetService<ICrisDirectoryServiceEngine>();
            if( crisEngineService == null ) return CSCodeGenerationResult.Retry;

            scope.Definition.Modifiers |= Modifiers.Sealed;

            using var scopeRegion = scope.Region();

            CreateExecutorMethods( classType, scope );

            scope.Append( """
                static CK.Cris.ICrisResultError HandleCommandError( IServiceProvider services, CK.Cris.ICrisPoco c, Exception ex, UserMessageCollector? v )
                {
                    var error = new CK.Cris.ICrisResultError_CK();
                    var monitor = (IActivityMonitor?)services.GetService( typeof( IActivityMonitor ) );
                    if( monitor == null )
                    {
                        error.Errors.Add( new UserMessage( UserMessageLevel.Error,
                                                           MCString.CreateNonTranslatable( NormalizedCultureInfo.CodeDefault,
                                                                                           "Missing monitor in Services. This is more than a critical error." ) ) );
                        error.Errors.Add( new UserMessage( UserMessageLevel.Error,
                                                           MCString.CreateNonTranslatable( NormalizedCultureInfo.CodeDefault,
                                                                                           ex.Message ) ) );
                    }
                    else
                    {
                        var currentCulture = (CurrentCultureInfo?)services.GetService( typeof( CurrentCultureInfo ) );
                        error.LogKey = PocoFactoryExtensions.OnUnhandledError( monitor, ex, (CK.Cris.IAbstractCommand)c, v == null, currentCulture, error.Errors.Add );
                    }
                    if( v != null )
                    {
                        error.IsValidationError = true;
                        v.UserMessages.AddRange( error.Errors );
                    }
                    return error;
                }


                static CK.Cris.ICrisResultError HandleHandlingValidationError( IServiceProvider s, CK.Cris.ICrisPoco c, UserMessageCollector v )
                {
                    var e = new CK.Cris.ICrisResultError_CK();
                    e.Errors.AddRange( v.UserMessages.Where( m => m.Level == UserMessageLevel.Error ) );
                    e.IsValidationError = true;
                    e.LogKey = LogValidationError( s, c, v );
                    return e;
                }

                """ );

            // Creates the static handlers functions.
            foreach( var e in crisEngineService.CrisTypes )
            {
                var h = e.CommandHandler;
                if( h != null )
                {
                    CreateCommandHandler( c.CurrentRun.EngineMap, scope, e, h );
                }
                else if( e.EventHandlers.Count > 0 )
                {
                    CreateEventHandler( c.CurrentRun.EngineMap, scope, e );
                }
            }

            // To accommodate RoutedEventHandler void/Task returns, the array of handlers returns a Task instead of
            // Task<object>. The RawExecuteAsync method downcasts the command entries to Task<RawResult>.
            // The DispatchEventAsync uses mere tasks. This enables an optimization for events with a single
            // asynchronous handler.
            const string funcSignature = "Func<IServiceProvider, CK.Cris.ICrisPoco, Task>";
            scope.Append( "readonly " ).Append( funcSignature ).Append( "[] _handlers = new " ).Append( funcSignature ).Append( "[]{" );
            foreach( var e in crisEngineService.CrisTypes )
            {
                if( e.CrisPocoIndex != 0 ) scope.Append( ", " );
                if( e.IsHandled )
                {
                    scope.Append( "H" ).Append( e.CrisPocoIndex );
                }
                else
                {
                    scope.Append( "null" );
                }
            }
            scope.Append( "};" ).NewLine();

            return CSCodeGenerationResult.Success;
        }

        static void CreateEventHandler( IStObjMap engineMap, ITypeScope scope, CrisType e )
        {
            var func = scope.CreateFunction( $"static Task H{e.CrisPocoIndex}(IServiceProvider s, CK.Cris.ICrisPoco c)" );

            var cachedServices = new VariableCachedServices( engineMap, func.CreatePart() );

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
            Throw.DebugAssert( syncHandlerCount <= e.EventHandlers.Count );
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

        static void CreateCommandHandler( IStObjEngineMap engineMap, ITypeScope scope, CrisType e, HandlerMethod h )
        {
            bool isVoidReturn = h.UnwrappedReturnType == typeof( void );
            bool isHandlerAsync = h.IsRefAsync || h.IsValAsync;
            bool isPostHandlerAsync = e.HasPostHandlerAsyncCall;

            var f = scope.CreateFunction( $"static Task<RawResult> H{e.CrisPocoIndex}( IServiceProvider s, CK.Cris.ICrisPoco c )" );

            var cachedServices = new VariableCachedServices( engineMap, f );

            f.Append( "UserMessageCollector? v = null;" ).NewLine();

            // Only if async is required we add the async modifier.
            bool isOverallAsync = isHandlerAsync || isPostHandlerAsync;

            f.Append( "try" )
             .OpenBlock();

            if( e.HandlingValidators.Count > 0 )
            {
                f.GeneratedByComment( $"There are {e.HandlingValidators.Count} validators." );
                f.Append( "v = new UserMessageCollector( " ).Append( cachedServices.GetServiceVariableName( typeof( CurrentCultureInfo ) ) ).Append( " );" ).NewLine()
                 .Append( "try" )
                 .OpenBlock();
                RawCrisReceiverImpl.GenerateValidationCode( f, e.HandlingValidators, cachedServices, out var validatorsRequireAsync );
                isOverallAsync |= validatorsRequireAsync;

                f.CloseBlock()
                 .Append( "catch( Exception ex )" )
                 .OpenBlock()
                 .Append( "var e = HandleCommandError( s, c, ex, v );" ).NewLine();
                WriteReturn( f, isOverallAsync, "e" );
                f.CloseBlock();

                f.Append( """
                    if( v.UserMessages.Count > 0 )
                    {
                        if( v.ErrorCount > 0 )
                        {
                            var e = HandleHandlingValidationError( s, c, v );
                    """ );
                WriteReturn( f, isOverallAsync, "e" );
                f.Append( """
                        }
                    }
                    else
                    {
                        v = null;
                    }
                    """ );
            }

            if( isOverallAsync ) f.Definition.Modifiers |= Modifiers.Async;

            cachedServices.StartNewCachedVariablesPart();

            if( !isVoidReturn ) f.AppendGlobalTypeName( e.CommandResultType?.Type ).Append( " r = " );
            if( isHandlerAsync ) f.Append( "await " );
            InlineCallOwnerMethod( f, h, cachedServices, h.CommandParameter ).Append( ";" ).NewLine();

            cachedServices.StartNewCachedVariablesPart();
            e.GeneratePostHandlerCallCode( f, cachedServices );

            WriteReturn( f, isOverallAsync, isVoidReturn ? "null" : "r" );

            f.CloseBlock()
             .Append( "catch( Exception ex )" )
             .OpenBlock();
            f.Append( "var e = HandleCommandError( s, c, ex, null );" ).NewLine();
            WriteReturn( f, isOverallAsync, "e" );
            f.CloseBlock();

            static void WriteReturn( IFunctionScope f, bool isOverallAsync, string result )
            {
                var raw = $"new RawResult( {result}, v )";
                if( isOverallAsync )
                {
                    f.Append( "return " ).Append( raw );
                }
                else
                {
                    f.Append( "return Task.FromResult( " ).Append( raw ).Append( " )" );
                }
                f.Append( ";" );
            }
        }

        static ICodeWriter InlineCallOwnerMethod( ICodeWriter w, HandlerBase h, VariableCachedServices cachedServices, ParameterInfo crisPocoParameter )
        {
            cachedServices.WriteExactType( w, h.Method.DeclaringType, h.Owner.ClassType ).Append( "." ).Append( h.Method.Name ).Append( "( " );
            foreach( var p in h.Parameters )
            {
                if( p.Position > 0 ) w.Append( ", " );
                if( p == crisPocoParameter )
                {
                    w.Append( "(" ).Append( crisPocoParameter.ParameterType.ToGlobalTypeName() ).Append( ")c" );
                }
                else
                {
                    w.Append( cachedServices.GetServiceVariableName( p.ParameterType ) );
                }
            }
            w.Append( " )" );
            return w;
        }

        static void CreateExecutorMethods( Type classType, ITypeScope scope )
        {
            using var _ = scope.Region();
            var rawExecuteMethod = classType.GetMethod( nameof( RawCrisExecutor.RawExecuteAsync ), new[] { typeof( IServiceProvider ), typeof( IAbstractCommand ) } );
            Throw.DebugAssert( rawExecuteMethod != null );

            // RawExecuteAsync is a direct relay by index in the _handlers array.
            var mExecute = scope.CreateSealedOverride( rawExecuteMethod );
            mExecute.Append( """
                        var h = _handlers[command.CrisPocoModel.CrisPocoIndex];
                        if( h == null )
                        {
                            var e = new CK.Cris.ICrisResultError_CK();
                            var c = (CurrentCultureInfo?)services.GetService( typeof( CurrentCultureInfo ) );
                            UserMessage msg = c != null
                                                ? UserMessage.Error( c, $"Command '{command.CrisPocoModel.PocoName}' has no command handler.", "Cris.MissingCommandHandler" )
                                                : UserMessage.Error( NormalizedCultureInfo.CodeDefault, $"Command '{command.CrisPocoModel.PocoName}' has no command handler.", "Cris.MissingCommandHandler" );
                            e.Errors.Add( msg );
                            ((IActivityLineEmitter?)services.GetService( typeof( IActivityMonitor ) ) ?? ActivityMonitor.StaticLogger)?.Error( msg.Message.CodeString.Text );
                            return Task.FromResult( new RawResult( e, null ) );
                        }
                        return global::System.Runtime.CompilerServices.Unsafe.As<Task<RawResult>>( h( services, command ) );

                        """ );

            // DispatchEventAsync is a direct relay by index in the _handlers array.
            // When handler is null, nothing is done.
            Throw.DebugAssert( nameof( RawCrisExecutor.DispatchEventAsync ) == "DispatchEventAsync" );
            Throw.DebugAssert( classType.GetMethod( nameof( RawCrisExecutor.DispatchEventAsync ), new[] { typeof( IServiceProvider ), typeof( IEvent ) } ) != null );
            var mDispatchEvent = scope.CreateFunction( "public override Task DispatchEventAsync( IServiceProvider s, CK.Cris.IEvent e )" );
            mDispatchEvent.Append( "return _handlers[e.CrisPocoModel.CrisPocoIndex]?.Invoke( s, e );" );

            // SafeDispatchEventAsync protects the direct relay by index in the _handlers array.
            // When handler is null, nothing is done and its okay.
            Throw.DebugAssert( nameof( RawCrisExecutor.SafeDispatchEventAsync ) == "SafeDispatchEventAsync" );
            Throw.DebugAssert( classType.GetMethod( nameof( RawCrisExecutor.SafeDispatchEventAsync ), new[] { typeof( IServiceProvider ), typeof( IEvent ) } ) != null );
            var mSafeDispatchEvent = scope.CreateFunction( "public override async Task<bool> SafeDispatchEventAsync( IServiceProvider s, CK.Cris.IEvent e )" );
            mSafeDispatchEvent.Append( """
                             var h = _handlers[e.CrisPocoModel.CrisPocoIndex];
                             if( h == null ) return true;
                             try
                             {
                                await h( s, e ).ConfigureAwait( false );
                                return true;
                             }
                             catch( Exception ex )
                             {
                                var monitor = (IActivityMonitor?)s.GetService( typeof(IActivityMonitor) );
                                var msg = $"Event '{e.CrisPocoModel.PocoName}' dispatch failed.";
                                if( monitor != null )
                                {
                                    using( monitor.OpenError( msg, ex ) )
                                    {
                                        monitor.Trace( e.ToString() );
                                    }
                                }
                                else
                                {
                                    ActivityMonitor.StaticLogger.Error( msg + " (No IActivityMonitor available.)", ex );
                                }
                                return false;
                             }
                             """ );
        }
    }

}
