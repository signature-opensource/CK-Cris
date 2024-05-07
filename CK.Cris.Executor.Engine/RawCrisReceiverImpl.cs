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
                                                            typeof( IAbstractCommand )
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
                         .Append( "return ((ICrisReceiverImpl)command).IncomingValidateAsync( monitor, validationContext, services );" );

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
                        var cachedServices = new VariableCachedServices( c.CurrentRun.EngineMap, f );

                        bool requiresAsync = false;
                        if( e.IncomingValidators.Count > 0 )
                        {
                            GenerateMultiTargetCalls( f, e.IncomingValidators, cachedServices, "v", hasMonitorParam: true, out requiresAsync );
                        }
                        string varHub = "null";
                        if( e.AmbientServicesConfigurators.Count > 0 )
                        {
                            varHub = "hub";
                            cachedServices.StartNewCachedVariablesPart();
                            f.Append( "var hub = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AmbientServiceHub>( s ).CleanClone();" ).NewLine();
                            GenerateMultiTargetCalls( f, e.AmbientServicesConfigurators, cachedServices, "hub", hasMonitorParam: true, out var configureRequiresAsync );
                            requiresAsync |= configureRequiresAsync;
                            f.Append( "if( !hub.IsDirty ) hub = null;" ).NewLine();
                        }
                        if( requiresAsync )
                        {
                            f.Definition.Modifiers |= Modifiers.Async;
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
                                                      IReadOnlyList<HandlerMultiTargetMethod> validators,
                                                      VariableCachedServices cachedServices,
                                                      string argumentParameterName,
                                                      bool hasMonitorParam,
                                                      out bool requiresAsync )
        {
            requiresAsync = false;
            if( validators.Count == 0 ) return;

            using var _ = f.Region();
            foreach( var validator in validators )
            {
                if( validator.IsRefAsync || validator.IsValAsync )
                {
                    f.Append( "await " );
                    requiresAsync = true;
                }
                cachedServices.WriteExactType( f, validator.Method.DeclaringType, validator.Owner.ClassType ).Append(".").Append( validator.Method.Name ).Append( "( " );
                foreach( var p in validator.Parameters )
                {
                    if( p.Position > 0 ) f.Append( ", " );
                    if( hasMonitorParam && typeof( IActivityMonitor ).IsAssignableFrom( p.ParameterType ) )
                    {
                        f.Append( "monitor" );
                    }
                    else if( p == validator.ThisPocoParameter )
                    {
                        f.Append( "(" ).AppendGlobalTypeName( validator.ThisPocoParameter.ParameterType ).Append( ")this" );
                    }
                    else if( p == validator.ArgumentParameter )
                    {
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
