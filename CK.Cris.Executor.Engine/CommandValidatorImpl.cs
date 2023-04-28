using CK.CodeGen;
using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup.Cris
{
    public class CommandValidatorImpl : CSCodeGeneratorType
    {
        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            Throw.CheckArgument( "Applies only to the CommandValidator class.", classType == typeof( CommandValidator ) );
            var registry = CommandRegistry.FindOrCreate( monitor, c );
            if( registry == null ) return CSCodeGenerationResult.Failed;

            var validateMethod = classType.GetMethod( nameof( CommandValidator.ValidateCommandAsync ), new[] { typeof( IActivityMonitor ), typeof( IServiceProvider ), typeof( ICrisPoco ) } );
            Debug.Assert( validateMethod != null, "This is the signature of the central method." );

            var mValidate = scope.CreateSealedOverride( validateMethod );
            if( !registry.CrisPocoModels.Any( e => e.Validators.Count > 0 ) )
            {
                mValidate.Definition.Modifiers &= ~Modifiers.Async;
                mValidate.GeneratedByComment().NewLine()
                         .Append( "return CK.Cris.CommandValidationResult.SuccessResultTask;" );
            }
            else
            {
                const string funcSignature = "Func<IActivityMonitor, IServiceProvider, CK.Cris.ICrisPoco, Task<CK.Cris.CommandValidationResult>>";

                scope.GeneratedByComment().NewLine()
                     .Append( "static readonly " ).Append( funcSignature ).Append( " Success = ( m, s, c ) => CK.Cris.CommandValidationResult.SuccessResultTask;" )
                     .NewLine();

                foreach( var e in registry.CrisPocoModels )
                {
                    if( e.Validators.Count > 0 )
                    {
                        bool requiresAsync = false;
                        var f = scope.CreateFunction( "static Task<CK.Cris.CommandValidationResult> V" + e.CrisPocoIndex + "( IActivityMonitor m, IServiceProvider s, CK.Cris.ICrisPoco c )" );

                        f.GeneratedByComment().NewLine();
                        var cachedServices = new VariableCachedServices( f.CreatePart() );
                        f.Append( "using( m.CollectEntries( out var entries, LogLevelFilter.Warn ) )" ).NewLine()
                         .OpenBlock()
                         .Append( "m.MinimalFilter = new LogFilter( LogLevelFilter.Warn, LogLevelFilter.Warn );" ).NewLine();

                        foreach( var service in e.Validators.GroupBy( v => v.Owner ) )
                        {
                            f.OpenBlock()
                             .Append( "var h = (" ).Append( service.Key.ClassType.ToCSharpName() ).Append( ")s.GetService(" ).AppendTypeOf( service.Key.ClassType ).Append( ");" ).NewLine();
                            foreach( var validator in service )
                            {
                                if( validator.IsRefAsync || validator.IsValAsync )
                                {
                                    f.Append( "await " );
                                    requiresAsync = true;
                                }
                                if( validator.Method.DeclaringType != service.Key.ClassType )
                                {
                                    f.Append( "((" ).Append( validator.Method.DeclaringType.ToCSharpName() ).Append( ")h)." );
                                }
                                else f.Append( "h." );
                                f.Append( validator.Method.Name ).Append( "( " );

                                foreach( var p in validator.Parameters )
                                {
                                    if( p.Position > 0 ) f.Append( ", " );
                                    if( typeof( IActivityMonitor ).IsAssignableFrom( p.ParameterType ) )
                                    {
                                        f.Append( "m" );
                                    }
                                    else if( p == validator.CmdOrPartParameter )
                                    {
                                        f.Append( "(" ).Append( validator.CmdOrPartParameter.ParameterType.ToCSharpName() ).Append( ")c" );
                                    }
                                    else
                                    {
                                        cachedServices.WriteGetService( f, p.ParameterType );
                                    }
                                }
                                f.Append( " );" ).NewLine();
                            }
                            f.CloseBlock();
                        }
                        if( requiresAsync ) f.Definition.Modifiers |= Modifiers.Async;
                        f.Append( "if( entries.Count == 0 ) return CK.Cris.CommandValidationResult.SuccessResult" ).Append( requiresAsync ? null : "Task" ).Append( ";" ).NewLine();
                        f.Append( "return " ).Append( requiresAsync
                                                        ? "CK.Cris.CommandValidationResult.Create( entries );"
                                                        : "Task.FromResult( CK.Cris.CommandValidationResult.Create( entries ) );" )
                         .CloseBlock();
                    }
                }

                scope.Append( "readonly " ).Append( funcSignature ).Append( "[] _validators = new " ).Append( funcSignature ).Append( "[" ).Append( registry.CrisPocoModels.Count ).Append( "]{" );
                foreach( var e in registry.CrisPocoModels )
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
                         .Append( "return _validators[command.CrisPocoModel.CrisPocoIndex]( validationMonitor, services, command );" );
            }
            return CSCodeGenerationResult.Success;
        }
    }

}
