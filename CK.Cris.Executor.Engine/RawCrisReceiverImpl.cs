using CK.CodeGen;
using CK.Core;
using CK.Cris;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup.Cris
{
    public class RawCrisReceiverImpl : CSCodeGeneratorType
    {
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            Throw.CheckArgument( "Applies only to the RawCrisReceiver class.", classType == typeof( RawCrisReceiver ) );

            var crisEngineService = c.CurrentRun.ServiceContainer.GetService<ICrisDirectoryServiceEngine>();
            if( crisEngineService == null ) return CSCodeGenerationResult.Retry;

            // DoIncomingValidateAsync is protected, we cannot use nameof() here.
            var validateMethod = classType.GetMethod( "DoIncomingValidateAsync",
                                                       System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance,
                                                       new[]
                                                        {
                                                            typeof( IActivityMonitor ),
                                                            typeof( UserMessageCollector ),
                                                            typeof( IServiceProvider ),
                                                            typeof( ICrisPoco )
                                                        } );
            Throw.DebugAssert( validateMethod != null );

            var mValidate = scope.CreateSealedOverride( validateMethod );
            if( !crisEngineService.CrisTypes.Any( e => e.IncomingValidators.Count > 0 ) )
            {
                mValidate.Definition.Modifiers &= ~Modifiers.Async;
                mValidate.GeneratedByComment().NewLine()
                         .Append( "return ValueTask.FromResult<AmbientServiceHub?>( null );" );
            }
            else
            {
                mValidate.GeneratedByComment().NewLine()
                         .Append( "return ((ICrisReceiverImpl)crisPoco).IncomingValidateAsync( monitor, validationContext, services );" );

                Throw.DebugAssert( scope.Namespace.FullName == "CK.Cris" );
                scope.Namespace.Append( """
                    [StObjGen]
                    interface ICrisReceiverImpl
                    {
                        // Use Default Implementation Method when no incoming validator exists and no AmbientServiceHub is nedded.
                        ValueTask<AmbientServiceHub?> IncomingValidateAsync( IActivityMonitor monitor, UserMessageCollector v, IServiceProvider s ) => ValueTask.FromResult<AmbientServiceHub?>( null );
                    }

                    """ );

                foreach( var e in crisEngineService.CrisTypes )
                {
                    var pocoType = c.GeneratedCode.FindOrCreateAutoImplementedClass( monitor, e.CrisPocoType.FamilyInfo.PocoClass );
                    pocoType.Definition.BaseTypes.Add( new ExtendedTypeName( "CK.Cris.ICrisReceiverImpl" ) );
                    if( e.IncomingValidators.Count > 0 || e.AmbientServicesConfigurators.Count > 0 )
                    {
                        var f = pocoType.CreateFunction( "ValueTask<AmbientServiceHub?> CK.Cris.ICrisReceiverImpl.IncomingValidateAsync( IActivityMonitor monitor, UserMessageCollector v, IServiceProvider s )" );
                        var cachedServices = new VariableCachedServices( c.CurrentRun.EngineMap, f, hasMonitor: true );

                        bool needAsyncStateMachine = e.IncomingValidators.AsyncHandlerCount > 0 || e.AmbientServicesConfigurators.AsyncHandlerCount > 0;
                        if( needAsyncStateMachine )
                        {
                            f.Definition.Modifiers |= Modifiers.Async;
                        }

                        if( e.IncomingValidators.Count > 0 )
                        {
                            GenerateMultiTargetCalls( f, e.IncomingValidators, cachedServices, "v" );
                        }
                        string varHub = "null";
                        if( e.AmbientServicesConfigurators.Count > 0 )
                        {
                            varHub = "hub";
                            cachedServices.StartNewCachedVariablesPart();
                            f.Append( "var hub = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AmbientServiceHub>( s ).CleanClone();" ).NewLine();
                            GenerateMultiTargetCalls( f, e.AmbientServicesConfigurators, cachedServices, "hub" );
                            f.Append( "if( !hub.IsDirty ) hub = null;" ).NewLine();
                        }
                        if( needAsyncStateMachine )
                        {
                            f.Append( "return " ).Append( varHub ).Append( ";" ).NewLine();
                        }
                        else
                        {
                            f.Append( "return ValueTask.FromResult<AmbientServiceHub?>(" ).Append( varHub ).Append( ");" ).NewLine();
                        }
                    }
                }
            }
            return CSCodeGenerationResult.Success;
        }

        internal static void GenerateMultiTargetCalls( IFunctionScope f,
                                                       MultiTargetHandlerList handlers,
                                                       VariableCachedServices cachedServices,
                                                       string? argumentParameterName )
        {
            if( handlers.Count == 0 ) return;

            using var _ = f.Region();
            foreach( var h in handlers )
            {
                if( h.IsRefAsync || h.IsValAsync )
                {
                    f.Append( "await " );
                }
                cachedServices.WriteExactType( f, h.Method.DeclaringType, h.Owner.ClassType ).Append(".").Append( h.Method.Name ).Append( "( " );
                foreach( var p in h.Parameters )
                {
                    if( p.Position > 0 ) f.Append( ", " );
                    if( p == h.ThisPocoParameter )
                    {
                        f.Append( "(" ).AppendGlobalTypeName( h.ThisPocoParameter.ParameterType ).Append( ")this" );
                    }
                    else if( p == h.ArgumentParameter )
                    {
                        Throw.DebugAssert( argumentParameterName != null );
                        f.Append( argumentParameterName );
                    }
                    else
                    {
                        f.Append( cachedServices.GetServiceVariableName( p.ParameterType ) );
                    }
                }
                f.Append( " );" ).NewLine();
            }
        }
    }

}
