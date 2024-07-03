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

            // sealed RawCrisExecutor_CK.
            scope.Definition.Modifiers |= Modifiers.Sealed;

            Throw.DebugAssert( scope.Namespace.FullName == "CK.Cris" );
            scope.Namespace.Append( """
                    [StObjGen]
                    interface ICrisExecutorImpl : ICrisPoco
                    {
                        // Use Default Implementation Method when no command handler has been found.
                        // We need the CrisPocoModel.PocoName: this is why this specializes ICrisPoco.
                        Task<RawCrisExecutor.RawResult> ExecCommandAsync( IServiceProvider s )
                        {
                            var e = new CK.Cris.ICrisResultError_CK();
                            var c = (CurrentCultureInfo?)s.GetService( typeof( CurrentCultureInfo ) );
                            UserMessage msg = c != null
                                                ? UserMessage.Error( c, $"Command '{CrisPocoModel.PocoName}' has no command handler.", "Cris.MissingCommandHandler" )
                                                : UserMessage.Error( NormalizedCultureInfo.CodeDefault, $"Command '{CrisPocoModel.PocoName}' has no command handler.", "Cris.MissingCommandHandler" );
                            e.Errors.Add( msg );
                            ((IActivityLineEmitter?)s.GetService( typeof( IActivityMonitor ) ) ?? ActivityMonitor.StaticLogger)?.Error( msg.Message.CodeString.Text );
                            return Task.FromResult( new RawCrisExecutor.RawResult( e, null ) );
                        }

                        // No event handlers => nothing to do.
                        Task DispatchEventAsync( IServiceProvider s ) => Task.CompletedTask;

                        // No service restorers => nothing to do.
                        ValueTask<(ICrisResultError?,AmbientServiceHub?)> RestoreAsync( IActivityMonitor monitor ) => ValueTask.FromResult<(ICrisResultError?,AmbientServiceHub?)>( (null,null) );
                    }
                    
                    """ );

            var restoreMethod = classType.GetMethod( nameof( RawCrisExecutor.RestoreAmbientServicesAsync ), new[] { typeof( IActivityMonitor ), typeof( ICrisPoco ) } );
            Throw.DebugAssert( restoreMethod != null );
            var mRestore = scope.CreateSealedOverride( restoreMethod );
            mRestore.Append( "return ((ICrisExecutorImpl)crisPoco).RestoreAsync( monitor );" );

            var executeMethod = classType.GetMethod( nameof( RawCrisExecutor.RawExecuteAsync ), new[] { typeof( IServiceProvider ), typeof( IAbstractCommand ) } );
            Throw.DebugAssert( executeMethod != null );
            var mExecute = scope.CreateSealedOverride( executeMethod );
            mExecute.Append( "return ((ICrisExecutorImpl)command).ExecCommandAsync( services );" );

            var dispatchMethod = classType.GetMethod( nameof( RawCrisExecutor.DispatchEventAsync ), new[] { typeof( IServiceProvider ), typeof( IEvent ) } );
            Throw.DebugAssert( dispatchMethod != null );
            var mDispatch = scope.CreateSealedOverride( dispatchMethod );
            mDispatch.Append( "return ((ICrisExecutorImpl)e).DispatchEventAsync( services );" );

            bool needUnexpectedErrorHelper = false;
            foreach( var e in crisEngineService.CrisTypes )
            {
                var pocoType = c.GeneratedCode.FindOrCreateAutoImplementedClass( monitor, e.CrisPocoType.FamilyInfo.PocoClass );
                pocoType.Definition.BaseTypes.Add( new ExtendedTypeName( "CK.Cris.ICrisExecutorImpl" ) );
                if( e.CommandHandler != null )
                {
                    needUnexpectedErrorHelper = true;
                    var f = pocoType.CreateFunction( "Task<CK.Cris.RawCrisExecutor.RawResult> CK.Cris.ICrisExecutorImpl.ExecCommandAsync( IServiceProvider s )" );
                    CreateCommandHandler( c.CurrentRun.EngineMap, f, e );
                }
                else if( e.EventHandlers.Count > 0 )
                {
                    var f = pocoType.CreateFunction( "Task CK.Cris.ICrisExecutorImpl.DispatchEventAsync( IServiceProvider s )" );
                    CreateEventHandler( c.CurrentRun.EngineMap, f, e );
                }
                if( e.AmbientServicesRestorers.Count > 0 )
                {
                    var f = pocoType.CreateFunction( "ValueTask<(CK.Cris.ICrisResultError?,AmbientServiceHub?)> CK.Cris.ICrisExecutorImpl.RestoreAsync( IActivityMonitor monitor )" );
                    CreateRestore( c.CurrentRun.EngineMap, f, e );
                }
            }
            if( needUnexpectedErrorHelper )
            {
                scope.Append( """
                internal static CK.Cris.ICrisResultError HandleCrisUnexpectedError( IServiceProvider services, CK.Cris.ICrisPoco c, IActivityMonitor? monitor, Exception ex, UserMessageCollector? v )
                {
                    var error = new CK.Cris.ICrisResultError_CK();
                    monitor ??= (IActivityMonitor?)services.GetService( typeof( IActivityMonitor ) );
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


                internal static CK.Cris.ICrisResultError HandleHandlingValidationError( IServiceProvider s, CK.Cris.ICrisPoco c, UserMessageCollector v )
                {
                    var e = new CK.Cris.ICrisResultError_CK();
                    e.Errors.AddRange( v.UserMessages.Where( m => m.Level == UserMessageLevel.Error ) );
                    e.IsValidationError = true;
                    e.LogKey = LogValidationError( s, c, v );
                    return e;
                }

                """ );
            }

            return CSCodeGenerationResult.Success;
        }

        static void CreateEventHandler( IStObjMap engineMap, IFunctionScope f, CrisType e )
        {
            var cachedServices = new VariableCachedServices( engineMap, f, false );
            f.GeneratedByComment();
            int syncHandlerCount = 0;
            foreach( var calls in e.EventHandlers.Where( h => !h.IsRefAsync && !h.IsValAsync ).GroupBy( h => h.Owner ) )
            {
                foreach( var h in calls )
                {
                    InlineCallOwnerMethod( f, h, cachedServices, h.EventOrPartParameter ).Append( ";" ).NewLine();
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
                    f.Append( "// No async state machine required." ).NewLine()
                         .Append( "return " );
                    InlineCallOwnerMethod( f, h, cachedServices, h.EventOrPartParameter )
                        .Append( h.IsValAsync ? ".AsTask();" : ";" );
                    return;
                }
                f.Definition.Modifiers |= Modifiers.Async;
                foreach( var calls in e.EventHandlers.Where( h => h.IsRefAsync || h.IsValAsync ).GroupBy( h => h.Owner ) )
                {
                    foreach( var h in calls )
                    {
                        f.Append( "await " );
                        InlineCallOwnerMethod( f, h, cachedServices, h.EventOrPartParameter ).Append( ";" ).NewLine();
                    }
                }
            }
            else
            {
                f.Append( "return Task.CompletedTask;" );
            }
        }

        static void CreateCommandHandler( IStObjEngineMap engineMap, IFunctionScope f, CrisType e )
        {
            HandlerMethod? h = e.CommandHandler;
            Throw.DebugAssert( h != null );
            bool isVoidReturn = h.UnwrappedReturnType == typeof( void );
            bool isHandlerAsync = h.IsRefAsync || h.IsValAsync;
            bool isPostHandlerAsync = e.HasPostHandlerAsyncCall;

            f.Append( "UserMessageCollector? v = null;" ).NewLine();

            // Only if async is required we add the async modifier.
            bool isOverallAsync = isHandlerAsync || isPostHandlerAsync;

            f.Append( "try" )
             .OpenBlock();

            var cachedServices = new VariableCachedServices( engineMap, f, false );

            if( e.HandlingValidators.Count > 0 )
            {
                isOverallAsync |= e.HandlingValidators.AsyncHandlerCount > 0;
                f.GeneratedByComment( $"There are {e.HandlingValidators.Count} validators." );
                f.Append( "v = new UserMessageCollector( " ).Append( cachedServices.GetServiceVariableName( typeof( CurrentCultureInfo ) ) ).Append( " );" ).NewLine()
                 .Append( "try" )
                 .OpenBlock();
                RawCrisReceiverImpl.GenerateMultiTargetCalls( f, e.HandlingValidators, cachedServices, "v" );
                f.CloseBlock()
                 .Append( "catch( Exception ex )" )
                 .OpenBlock()
                 .Append( "var e = CK.Cris.RawCrisExecutor_CK.HandleCrisUnexpectedError( s, this, null, ex, v );" ).NewLine();
                WriteReturn( f, isOverallAsync, "e" );
                f.CloseBlock();

                f.Append( """
                    if( v.UserMessages.Count > 0 )
                    {
                        if( v.ErrorCount > 0 )
                        {
                            var e = CK.Cris.RawCrisExecutor_CK.HandleHandlingValidationError( s, this, v );
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

            if( e.PostHandlers.Count > 0 )
            {
                GeneratePostHandlerCallCode( f, e, cachedServices );
            }
            WriteReturn( f, isOverallAsync, isVoidReturn ? "null" : "r" );

            f.CloseBlock()
             .Append( "catch( Exception ex )" )
             .OpenBlock();
            f.Append( "var e = CK.Cris.RawCrisExecutor_CK.HandleCrisUnexpectedError( s, this, null, ex, null );" ).NewLine();
            WriteReturn( f, isOverallAsync, "e" );
            f.CloseBlock();

            static void WriteReturn( IFunctionScope f, bool isOverallAsync, string result )
            {
                var raw = $"new CK.Cris.RawCrisExecutor.RawResult( {result}, v )";
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


            static void GeneratePostHandlerCallCode( IFunctionScope w, CrisType e, VariableCachedServices cachedServices )
            {
                if( e.PostHandlers.Count == 0 ) return;

                using var region = w.Region();
                cachedServices.StartNewCachedVariablesPart();
                foreach( var h in e.PostHandlers.Where( h => !h.IsRefAsync && !h.IsValAsync ).GroupBy( h => h.Owner ) )
                {
                    CreateOwnerCalls( w, h, false, cachedServices );
                }
                cachedServices.StartNewCachedVariablesPart();
                foreach( var h in e.PostHandlers.Where( h => h.IsRefAsync || h.IsValAsync ).GroupBy( h => h.Owner ) )
                {
                    CreateOwnerCalls( w, h, true, cachedServices );
                }

                static void CreateOwnerCalls( ICodeWriter w,
                                              IGrouping<IStObjFinalClass, HandlerPostMethod> oH,
                                              bool async,
                                              VariableCachedServices cachedServices )
                {
                    foreach( HandlerPostMethod m in oH )
                    {
                        if( async ) w.Append( "await " );

                        cachedServices.WriteExactType( w, m.Method.DeclaringType, oH.Key.ClassType ).Append( "." ).Append( m.Method.Name ).Append( "( " );
                        foreach( ParameterInfo p in m.Parameters )
                        {
                            if( p.Position > 0 ) w.Append( ", " );
                            if( p == m.ResultParameter )
                            {
                                if( m.MustCastResultParameter )
                                {
                                    w.Append( "(" ).AppendGlobalTypeName( p.ParameterType ).Append( ")" );
                                }
                                w.Append( "r" );
                            }
                            else if( p == m.CmdOrPartParameter )
                            {
                                w.Append( "(" ).AppendGlobalTypeName( m.CmdOrPartParameter.ParameterType ).Append( ")this" );
                            }
                            else
                            {
                                w.Append( cachedServices.GetServiceVariableName( p.ParameterType ) );
                            }
                        }
                        w.Append( " );" ).NewLine();
                    }
                }
            }

        }

        static void CreateRestore( IStObjEngineMap engineMap, IFunctionScope f, CrisType e )
        {
            bool requiresAsync = e.AmbientServicesRestorers.AsyncHandlerCount > 0;
            if( requiresAsync )
            {
                f.Definition.Modifiers |= Modifiers.Async;
            }
            f.Append( "var s = DIContainerHub_CK.GlobalServices;" ).NewLine()
             .Append( "try" )
             .OpenBlock();
            var cachedServices = new VariableCachedServices( engineMap, f, hasMonitor: true );
            f.Append( "var hub = new AmbientServiceHub_CK();" ).NewLine();
            RawCrisReceiverImpl.GenerateMultiTargetCalls( f, e.AmbientServicesRestorers, cachedServices, "hub" );
            WriteReturn( f, requiresAsync, "(null,hub)" );
            f.CloseBlock()
             .Append( "catch( Exception ex )" )
             .OpenBlock();
            f.Append( "var e = CK.Cris.RawCrisExecutor_CK.HandleCrisUnexpectedError( s, this, monitor, ex, null );" ).NewLine();
            WriteReturn( f, requiresAsync, "(e,null)" );
            f.CloseBlock();

            static void WriteReturn( IFunctionScope f, bool requiresAsync, string result )
            {
                if( requiresAsync )
                {
                    f.Append( "return " ).Append( result );
                }
                else
                {
                    f.Append( "return ValueTask.FromResult<(CK.Cris.ICrisResultError?,AmbientServiceHub?)>( " ).Append( result ).Append( " )" );
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
                    w.Append( "(" ).Append( crisPocoParameter.ParameterType.ToGlobalTypeName() ).Append( ")this" );
                }
                else
                {
                    w.Append( cachedServices.GetServiceVariableName( p.ParameterType ) );
                }
            }
            w.Append( " )" );
            return w;
        }
    }

}
