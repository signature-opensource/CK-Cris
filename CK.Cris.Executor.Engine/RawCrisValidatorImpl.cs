using CK.CodeGen;
using CK.Core;
using CK.Cris;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup.Cris
{
    public class RawCrisValidatorImpl : CSCodeGeneratorType
    {
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            Throw.CheckArgument( "Applies only to the RawCrisValidator class.", classType == typeof( RawCrisValidator ) );

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
            Throw.DebugAssert( "This is the signature of the central method.", validateMethod != null );

            var mValidate = scope.CreateSealedOverride( validateMethod );
            if( !crisEngineService.CrisTypes.Any( e => e.Validators.Count > 0 ) )
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
                    if( e.Validators.Count > 0 )
                    {
                        var f = scope.CreateFunction( "static Task V" + e.CrisPocoIndex + "( IActivityMonitor m, UserMessageCollector v, IServiceProvider s, CK.Cris.IAbstractCommand c )" );

                        GenerateValidationCode( c.CurrentRun.EngineMap, f, e.Validators, out bool requiresAsync, out _ );
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

                scope.Append( "readonly " ).Append( funcSignature ).Append( "[] _validators = new " ).Append( funcSignature ).Append( "[" ).Append( crisEngineService.CrisTypes.Count ).Append( "]{" );
                foreach( var e in crisEngineService.CrisTypes )
                {
                    if( e.CrisPocoIndex != 0 ) scope.Append( ", " );
                    if( e.Validators.Count == 0 )
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

        internal static void GenerateValidationCode( IStObjEngineMap engineMap,
                                                     IFunctionScope f,
                                                     IEnumerable<HandlerValidatorMethod> validators,
                                                     out bool requiresAsync,
                                                     out VariableCachedServices cachedServices )
        {
            using var _ = f.Region();
            cachedServices = new VariableCachedServices( engineMap, f.CreatePart() );

            requiresAsync = false;
            foreach( var validator in validators )
            {
                var owner = cachedServices.GetServiceVariableName( validator.Owner.ClassType );
                if( validator.IsRefAsync || validator.IsValAsync )
                {
                    f.Append( "await " );
                    requiresAsync = true;
                }
                if( validator.Method.DeclaringType != validator.Owner.ClassType )
                {
                    f.Append( "((" ).AppendGlobalTypeName( validator.Method.DeclaringType ).Append( ")").Append( owner ).Append(")" );
                }
                else f.Append( owner );
                f.Append(".").Append( validator.Method.Name ).Append( "( " );

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
