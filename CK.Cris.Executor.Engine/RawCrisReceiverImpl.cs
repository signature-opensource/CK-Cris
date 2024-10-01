using CK.CodeGen;
using CK.Core;
using CK.Cris;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Implements the <see cref="RawCrisReceiver"/> concrete class.
    /// </summary>
    public class RawCrisReceiverImpl : CSCodeGeneratorType
    {
        /// <inheritdoc />
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            Throw.CheckArgument( "Applies only to the RawCrisReceiver class.", classType == typeof( RawCrisReceiver ) );

            var crisEngineService = c.CurrentRun.ServiceContainer.GetService<ICrisDirectoryServiceEngine>();
            if( crisEngineService == null ) return CSCodeGenerationResult.Retry;

            foreach( var e in crisEngineService.CrisTypes )
            {
                var pocoType = c.GeneratedCode.FindOrCreateAutoImplementedClass( monitor, e.CrisPocoType.FamilyInfo.PocoClass );
                pocoType.Definition.BaseTypes.Add( new ExtendedTypeName( "CK.Cris.RawCrisReceiver.ICrisReceiverImpl" ) );
                if( e.IncomingValidators.Count > 0 || e.AmbientServicesConfigurators.Count > 0 )
                {
                    var f = pocoType.CreateFunction( "ValueTask CK.Cris.RawCrisReceiver.ICrisReceiverImpl.IncomingValidateAsync( CK.Cris.RawCrisReceiver.ValidationContext c )" );
                    f.Append( "var monitor = c.Monitor;" ).NewLine()
                     .Append( "var s = c.Services;" ).NewLine();
                    var cachedServices = new VariableCachedServices( c.CurrentRun.EngineMap, f, hasMonitor: true );

                    bool needAsyncStateMachine = e.IncomingValidators.AsyncHandlerCount > 0 || e.AmbientServicesConfigurators.AsyncHandlerCount > 0;
                    if( needAsyncStateMachine )
                    {
                        f.Definition.Modifiers |= Modifiers.Async;
                    }

                    if( e.IncomingValidators.Count > 0 )
                    {
                        GenerateMultiTargetCalls( f, e.IncomingValidators, cachedServices, "c.Messages", "c" );
                    }
                    if( e.AmbientServicesConfigurators.Count > 0 )
                    {
                        cachedServices.StartNewCachedVariablesPart();
                        f.Append( "var hub = c.EnsureHub();" ).NewLine();
                        GenerateMultiTargetCalls( f, e.AmbientServicesConfigurators, cachedServices, "hub" );
                    }
                    if( !needAsyncStateMachine )
                    {
                        f.Append( "return ValueTask.CompletedTask;" );
                    }
                }
            }
            return CSCodeGenerationResult.Success;
        }

        internal static void GenerateMultiTargetCalls( IFunctionScope f,
                                                       MultiTargetHandlerList handlers,
                                                       VariableCachedServices cachedServices,
                                                       string argumentParameterName,
                                                       string? argumentParameterName2 = null )
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
                        f.Append( argumentParameterName );
                    }
                    else if( p == h.ArgumentParameter2 )
                    {
                        f.Append( argumentParameterName2 );
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
