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

            var validateMethod = classType.GetMethod( nameof( CommandValidator.ValidateCommandAsync ), new[] { typeof( IServiceProvider ), typeof( ICommand ) } );
            Debug.Assert( validateMethod != null, "This is the signature of the central method." );

            var mValidate = scope.CreateSealedOverride( validateMethod );
            if( registry.Commands.Any( e => e.Validators.Count > 0 ) )
            {
                const string funcSignature = "Func<IActivityMonitor, IServiceProvider, CK.Cris.ICommand, Task<CK.Cris.ValidationResult>>";
                scope.Append( "static readonly " ).Append( funcSignature ).Append( " Success = ( m, s, c ) => Task.FromResult( new CK.Cris.ValidationResult( c ) );" )
                     .NewLine();

                foreach( var e in registry.Commands )
                {
                    if( e.Validators.Count > 0 )
                    {
                        bool requiresAsync = false;
                        var f = scope.CreateFunction( "static Task<CK.Cris.ValidationResult> V" + e.CommandIdx + "( IActivityMonitor m, IServiceProvider s, CK.Cris.ICommand c )" );

                        f.GeneratedByComment().NewLine()
                         .Append( "using( m.CollectEntries( out var entries, LogLevelFilter.Warn ) )" ).NewLine()
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
                                    f.Append("((").Append( validator.Method.DeclaringType.ToCSharpName() ).Append( ")h)." );
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
                                        f.Append( "(" ).Append( p.ParameterType.ToCSharpName() ).Append( ")s.GetService(" ).AppendTypeOf( p.ParameterType ).Append( ")" );
                                    }
                                }
                                f.Append( " );" ).NewLine();
                            }
                            f.CloseBlock();
                        }
                        f.Append( "return " ).Append( requiresAsync ? "new CK.Cris.ValidationResult( entries, c );" : "Task.FromResult( new CK.Cris.ValidationResult( entries, c ) );" )
                         .CloseBlock();
                        if( requiresAsync ) f.Definition.Modifiers |= Modifiers.Async;
                    }
                }

                scope.Append( "readonly " ).Append( funcSignature ).Append( "[] _validators = new " ).Append( funcSignature ).Append( "[" ).Append( registry.Commands.Count ).Append( "]{" );
                foreach( var e in registry.Commands )
                {
                    if( e.CommandIdx != 0 ) scope.Append( ", " );
                    if( e.Validators.Count == 0 )
                    {
                        scope.Append( "Success" );
                    }
                    else
                    {
                        scope.Append( "V" ).Append( e.CommandIdx );
                    }
                }
                scope.Append( "};" )
                     .NewLine();

                mValidate.GeneratedByComment().NewLine()
                         .Append( "var m = (IActivityMonitor)s.GetService( typeof(IActivityMonitor) );" ).NewLine()
                         .Append( "Throw.CheckArgument( " )
                            .AppendSourceString( "A IActivityMonitor must be registered in the service provider." )
                            .Append( ", m != null );" ).NewLine()
                         .Append( "return _validators[command.CommandModel.CommandIdx]( m, services, command );" );
            }
            else
            {
                mValidate.Definition.Modifiers &= ~Modifiers.Async;
                mValidate.Append( "return Task.FromResult<CK.Cris.ValidationResult>( new CK.Cris.ValidationResult( command ) );" );
            }
            return CSCodeGenerationResult.Success;
        }

    }

}
