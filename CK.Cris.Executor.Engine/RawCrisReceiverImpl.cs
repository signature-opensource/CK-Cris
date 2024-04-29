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

            // DoValidateCommandAsync is protected, we cannot use nameof() here.
            var validateMethod = classType.GetMethod( "DoValidateCommandAsync",
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
            if( !crisEngineService.CrisTypes.Any( e => e.EndpointValidators.Count > 0 ) )
            {
                mValidate.Definition.Modifiers &= ~Modifiers.Async;
                mValidate.GeneratedByComment().NewLine()
                         .Append( "return Task.CompletedTask;" );
            }
            else
            {
                const string funcSignature = "Func<IActivityMonitor, UserMessageCollector, IServiceProvider, CK.Cris.IAbstractCommand, Task>";

                scope.GeneratedByComment().NewLine()
                     .Append( "static readonly " ).Append( funcSignature ).Append( " Success = ( m, v, s, c ) => CK.Cris.CrisValidationResult.SuccessResultTask;" )
                     .NewLine();

                foreach( var e in crisEngineService.CrisTypes )
                {
                    if( e.EndpointValidators.Count > 0 )
                    {
                        var f = scope.CreateFunction( $"static Task V{e.CrisPocoIndex}( IActivityMonitor m, UserMessageCollector v, IServiceProvider s, CK.Cris.IAbstractCommand c )" );

                        var cachedServices = new VariableCachedServices( c.CurrentRun.EngineMap, f );
                        GenerateValidationCode( f, e.EndpointValidators, cachedServices, out bool requiresAsync );
                        if( requiresAsync )
                        {
                            f.Definition.Modifiers |= Modifiers.Async;
                        }
                        else
                        {
                            f.Append( "return Task.CompletedTask;" ).NewLine();
                        }
                    }
                }

                scope.Append( "readonly " ).Append( funcSignature ).Append( "[] _validators = new " ).Append( funcSignature ).Append( "[]{" );
                foreach( var e in crisEngineService.CrisTypes )
                {
                    if( e.CrisPocoIndex != 0 ) scope.Append( ", " );
                    if( e.EndpointValidators.Count == 0 )
                    {
                        scope.Append( "Success" );
                    }
                    else
                    {
                        scope.Append( "V" ).Append( e.CrisPocoIndex );
                    }
                }
                scope.Append( "};" )
                     .NewLine();

                mValidate.GeneratedByComment().NewLine()
                         .Append( "return _validators[command.CrisPocoModel.CrisPocoIndex]( monitor, validationContext, services, command );" );
            }
            return CSCodeGenerationResult.Success;
        }

        internal static void GenerateValidationCode( IFunctionScope f,
                                                     IReadOnlyList<HandlerValidatorMethod> validators,
                                                     VariableCachedServices cachedServices,
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
                    if( typeof( IActivityMonitor ).IsAssignableFrom( p.ParameterType ) )
                    {
                        f.Append( "m" );
                    }
                    else if( p == validator.CmdOrPartParameter )
                    {
                        f.Append( "(" ).AppendGlobalTypeName( validator.CmdOrPartParameter.ParameterType ).Append( ")c" );
                    }
                    else if( p == validator.ValidationContextParameter )
                    {
                        f.Append( "v" );
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
