using CK.CodeGen;
using CK.CodeGen.Abstractions;
using CK.Core;
using CK.Cris;
using CK.Text;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CK.Setup.Cris
{
    public class CommandValidatorImpl : AutoImplementorType
    {
        public override bool Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
        {
            if( classType != typeof( CommandValidator ) ) throw new InvalidOperationException( "Applies only to the CommandValidator class." );
            var commands = CommandRegistry.FindOrCreate( monitor, c );
            if( commands == null ) return false;

            var mValidate = scope.CreateSealedOverride( classType.GetMethod( nameof(CommandValidator.ValidateCommandAsync), new[] { typeof( IActivityMonitor ), typeof( IServiceProvider ), typeof( KnownCommand ) } ) );
            if( commands.Commands.Any( e => e.Validators.Count > 0 ) )
            {
                const string funcSignature = "Func<IActivityMonitor, IServiceProvider, CK.Cris.KnownCommand, Task<CK.Cris.ValidationResult>>";
                scope.Append( "static readonly " ).Append( funcSignature ).Append( " Success = ( m, s, c ) => Task.FromResult( new CK.Cris.ValidationResult( c ) );" )
                     .NewLine();

                foreach( var e in commands.Commands )
                {
                    if( e.Validators.Count > 0 )
                    {
                        scope.Append( "static async Task<CK.Cris.ValidationResult> V" ).Append( e.CommandIdx ).Append( "( IActivityMonitor m, IServiceProvider s, CK.Cris.KnownCommand c )" ).NewLine()
                             .Append( "{" ).NewLine();

                        scope.Append( "IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries = Array.Empty<ActivityMonitorSimpleCollector.Entry>();" ).NewLine()
                             .Append( "using( m.CollectEntries( e => entries = e, LogLevelFilter.Warn ) )" ).NewLine()
                             .Append( "{" ).NewLine()
                             .Append( "m.MinimalFilter = new LogFilter( LogLevelFilter.Warn, LogLevelFilter.Warn );" ).NewLine();

                        foreach( var service in e.Validators.GroupBy( v => v.Owner ) )
                        {
                            scope.Append( "{" ).NewLine();
                            scope.Append( "var h = (" ).AppendCSharpName( service.Key.ClassType ).Append( ")s.GetService(" ).AppendTypeOf( service.Key.ClassType ).Append( ");" ).NewLine();
                            foreach( var validator in service )
                            {
                                if( validator.IsRefAsync || validator.IsValAsync ) scope.Append( "await " );
                                scope.Append( "h." ).Append( validator.Method.Name ).Append( "( " );

                                foreach( var p in validator.Parameters )
                                {
                                    if( p.Position > 0 ) scope.Append( ", " );
                                    if( typeof( IActivityMonitor ).IsAssignableFrom( p.ParameterType ) ) scope.Append( "m" );
                                    else if( p == validator.CommandParameter )
                                    {
                                        scope.Append( "(" ).AppendCSharpName( validator.CommandParameter.ParameterType ).Append( ")c.Command" );
                                    }
                                    else
                                    {
                                        scope.Append( "(" ).AppendCSharpName( p.ParameterType ).Append( ")s.GetService(" ).AppendTypeOf( p.ParameterType ).Append( ")" );
                                    }
                                }
                                scope.Append( " );" ).NewLine();
                            }
                            scope.Append( "}" ).NewLine();
                        }
                    }
                    scope.Append( "}" ).NewLine()
                         .Append( "return new CK.Cris.ValidationResult( entries, c );" ).NewLine();

                    scope.Append( "}" ).NewLine();
                }

                scope.Append( "readonly " ).Append( funcSignature ).Append( "[] _validators = new " ).Append( funcSignature ).Append( "[" ).Append( commands.Commands.Count ).Append( "]{" );
                foreach( var e in commands.Commands )
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

                mValidate.Append( "return _validators[command.Model.CommandIdx]( monitor, services, command );" );
            }
            else
            {
                mValidate.Append( "return Task.FromResult<CK.Cris.ValidationResult>( new CK.Cris.ValidationResult( command ) );" );
            }
            return true;
        }
    }

}
