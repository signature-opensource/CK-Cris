using CK.CodeGen;
using CK.Core;
using CK.Cris;
using CK.Setup.Cris;
using CK.StObj.TypeScript;
using CK.StObj.TypeScript.Engine;
using CK.TypeScript.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CK.Setup
{
    public class TypeScriptCrisCommandGeneratorImpl : ITSCodeGenerator
    {
        CommandRegistry? _registry;

        bool ITSCodeGenerator.ConfigureTypeScriptAttribute( IActivityMonitor monitor, ITSTypeFileBuilder builder, TypeScriptAttribute a )
        {
            // Nothing to do: we don't want to interfere with the standard IPoco handling.
            return true;
        }
        bool ITSCodeGenerator.Initialize( IActivityMonitor monitor, TypeScriptContext context )
        {
            _registry = CommandRegistry.Find( monitor, context.CodeContext );
            Debug.Assert( _registry != null, "CSharp code implementation has necessarily been successfully called." );
            context.PocoCodeGenerator.PocoGenerating += OnPocoGenerating;
            return true;
        }

        bool ITSCodeGenerator.GenerateCode( IActivityMonitor monitor, TypeScriptContext g )
        {
            Debug.Assert( _registry != null );
            // The ICrisResult and the ICrisErrorResult must be in TypeScript.
            g.DeclareTSType( monitor, typeof(ICrisResult) );
            g.DeclareTSType( monitor, typeof(ICrisResultError) );
            using( monitor.OpenInfo( $"Declaring TypeScript support for {_registry.Commands.Count} commands." ) )
            {
                foreach( var cmd in _registry.Commands )
                {
                    // Declares the IPoco and the command result.
                    // The TSIPocoCodeGenerator (in CK.StObj.TypeScript.Engine) generates all the
                    // interfaces and the final class implementation in the same file (file and class
                    // is the PrimaryInterface name without the I).
                    g.DeclareTSType( monitor, cmd.Command.PrimaryInterface );

                    // Declares the command result, whatever it is.
                    // If it's a IPoco, it will benefit from the same treatment as the command above.
                    // Some types are handled by default, but if there is eventually no generator for the
                    // type then the setup fails.
                    if( cmd.ResultType != typeof( void ) && cmd.ResultType != typeof( NoWaitResult ) )
                    {
                        g.DeclareTSType( monitor, cmd.ResultType );
                    }
                }
            }
            return true;
        }


        // We capture the list of the Ambient Values properties.
        IReadOnlyList<TypeScriptPocoPropertyInfo>? _ambientValuesProperties;

        /// <summary>
        /// This is where everything happens: if the Poco being generated is a ICommand, then:
        /// <list type="bullet">
        /// <item>
        /// We add the commandModel signature to all the IPoco interfaces and its implementation
        /// on the Poco implementation. It is the CommandModel&lt;TResult&gt; that carries the
        /// command result type (if any) so that type inference can be used to type the return
        /// of the send method.
        /// </item>
        /// <item>
        /// Poco properties that with same ambient values names are checked (their type must be assignable
        /// to the ambient value's type), their corresponding parameter is removed from the create factory
        /// method and the applyAmbientValues method (on the commandModel) applies them.
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="sender">The <see cref="TSIPocoCodeGenerator"/>.</param>
        /// <param name="e">The Poco generation information.</param>
        void OnPocoGenerating( object? sender, PocoGeneratingEventArgs e )
        {
            Debug.Assert( _registry != null, "CS code generation ran. Implement (CSharp code) has necessarily been successfully called." );

            if( typeof(ICommand).IsAssignableFrom( e.TypeFile.Type ) )
            {
                var cmd = _registry.Find( e.PocoClass.PocoRootInfo );
                // A IPoco that supports ICommand should be a registered command.
                // If it's not the case, this is weird but that MAY be possible.
                // Defensive programming here.
                if( cmd == null ) return;

                // Force the IAmbientValues to be registered first.
                if( e.EnsurePoco( _registry.AmbientValues ) == null )
                {
                    e.SetError( $"Since generating the IAmbientValues TypeScript failed, no command can be generated." );
                    return;
                }

                Debug.Assert( _ambientValuesProperties != null, "The PocoGeneratingEventArgs for IAmbientValues has been called." );

                EnsureCrisModel( e );

                bool isVoidReturn = cmd.ResultType == typeof( void );
                bool isFireAndForget = !isVoidReturn && cmd.ResultType == typeof( NoWaitResult );

                // Compute the (potentially) type name only once by caching the signature.
                var b = e.PocoClass.Part;
                string? signature = AppendCommandModelSignature( b, cmd, e );
                if( signature == null )
                {
                    e.SetError();
                    return;
                }
                b.Append( " = " )
                    .OpenBlock()
                        .Append( "commandName: " ).AppendSourceString( cmd.CommandName ).Append( "," ).NewLine()
                        .Append( "isFireAndForget: " ).Append( isFireAndForget ).Append( "," ).NewLine()
                        .Append( "applyAmbientValues: (values: { [index: string]: any }, force?: boolean ) => " )
                        .OpenBlock()
                            .Append( ApplyAmbientValues )
                        .CloseBlock()
                    .CloseBlock( withSemiColon: true );

                // All the interfaces share the commandModel signature.
                if( e.TypeFile.Context.Root.GeneratePocoInterfaces )
                {
                    foreach( var itf in e.PocoClass.PocoRootInfo.Interfaces )
                    {
                        var code = e.TypeFile.File.Body.FindKeyedPart( itf.PocoInterface );
                        Debug.Assert( code != null );
                        code.Append( signature ).Append( ";" ).NewLine();
                    }
                }

                void ApplyAmbientValues( ITSCodePart b )
                {
                    Debug.Assert( _ambientValuesProperties != null );
                    bool atLeastOne = false;
                    foreach( var a in _ambientValuesProperties )
                    {
                        // Find the property with the same (parameter) name.
                        var fromAmbient = e.PocoClass.Properties.FirstOrDefault( p => p.Property.Name == a.Property.Name );
                        if( fromAmbient != null )
                        {
                            // Documents it.
                            fromAmbient.Property.Comment += Environment.NewLine + "(This is an Ambient Value.)";
                            e.Monitor.Debug( $"Property '{fromAmbient.Property.Name}' is an ambient value." );
                            // Remove the parameter.
                            fromAmbient.CreateMethodParameter = null;
                            // Adds the assignment: this property comes from its ambient value.
                            if( atLeastOne ) b.NewLine();
                            if( fromAmbient.PocoProperty.IsReadOnly )
                            {
                                throw new NotImplementedException( "Read only properties ambient value assignment." );
                            }
                            else
                            {
                                b.Append( "if( force || typeof this." ).Append( fromAmbient.Property.Name ).Append( " === \"undefined\" ) this." ).Append( fromAmbient.Property.Name )
                                 .Append( " = values[" ).AppendSourceString( fromAmbient.CtorParameterName ).Append( "];" );
                            }
                            atLeastOne = true;
                        }
                    }
                    if( !atLeastOne && e.PocoClass.PocoRootInfo != _registry.AmbientValues ) b.Append( "// This command has no property that appear in the Ambient Values." ).NewLine();
                }
            }
            else if( e.PocoClass.PocoRootInfo == _registry.AmbientValues )
            {
                Debug.Assert( _ambientValuesProperties == null );
                _ambientValuesProperties = e.PocoClass.Properties;
            }
        }

        static string? AppendCommandModelSignature( ITSCodePart code, CommandRegistry.Entry cmd, PocoGeneratingEventArgs e )
        {
            var signature = "readonly " + (e.TypeFile.Context.Root.PascalCase ? "C" : "c") + "ommandModel: CommandModel<";
            code.Append( signature );
            var typeName = code.AppendAndGetComplexTypeName( e.Monitor, e.TypeFile.Context, cmd.ResultNullableTypeTree );
            if( typeName == null ) return null;
            signature += typeName + ">";
            code.Append( ">" );
            return signature;
        }

        static void EnsureCrisModel( PocoGeneratingEventArgs e )
        {
            var folder = e.TypeFile.Context.Root.Root.FindOrCreateFolder( "CK/Cris" );
            var fModel = folder.FindOrCreateFile( "Model.ts", out bool created );
            if( created )
            {
                InitializeCrisModelFile( e.Monitor, fModel );
            }
            e.TypeFile.File.Imports.EnsureImport( fModel, "CommandModel", "ICrisEndpoint" );
        }

        static void InitializeCrisModelFile( IActivityMonitor monitor, TypeScriptFile<TypeScriptContextRoot> fModel )
        {
            fModel.EnsureImport( monitor, typeof( VESACode ), typeof( ICrisResultError ), typeof(ICrisResult) );
            fModel.Imports.EnsureImportFromLibrary( new LibraryImport( "axios", "^1.1.3", DependencyKind.Dependency ),
                "Axios"
                );
            fModel.Body.Append( @"

export type ICommandResult<T> = {
    code: VESACode.Error | VESACode.ValidationError,
    result: CrisResultError,
    correlationId?: string
} |
{
    code: 'CommunicationError',
    result: Error
} |
{
    code: VESACode.Synchronous,
    result: T,
    correlationId?: string
};

export interface CommandModel<TResult> {
    readonly commandName: string;
    readonly isFireAndForget: boolean;
    applyAmbientValues: (values: { [index: string]: any }, force?: boolean ) => void;
}

export interface Command<TResult = void> {
  commandModel: CommandModel<TResult>;
}

export interface ICrisEndpoint {
 send<T>(command: Command<T>): Promise<ICommandResult<T>>;
}

export class HttpCrisEndpoint implements ICrisEndpoint {
  constructor(private readonly axios: Axios) {
  }

  async send<T>(command: Command<T>): Promise<ICommandResult<T>> {
    try {
      let string = `[""${command.commandModel.commandName}""`;
      string += `,${JSON.stringify(command, (key, value) => {
        return key == ""commandModel"" ? undefined : value;
      })}]`;
      const resp = await this.axios.post<string>('', string);

      const result = JSON.parse(resp.data)[1] as CrisResult; // TODO: @Dan implement io-ts.
      if (result.code == VESACode.Synchronous) {
        return {
          code: VESACode.Synchronous,
          result: result.result as T,
          correlationId: result.correlationId
        };
      }
      else if (result.code == VESACode.Error || result.code == VESACode.ValidationError) {
        return {
          code: result.code as VESACode.Error | VESACode.ValidationError,
          result: result.result as CrisResultError,
          correlationId: result.correlationId
        };
      }
      else if (result.code == VESACode.Asynchronous) {
        throw new Error(""Endpoint returned VESACode.Asynchronous which is not yet supported by this client."");
      } else {
        throw new Error(""Endpoint returned an unknown VESA Code."");
      }
    } catch (e) {
      if (e instanceof Error) {
        return {
          code: 'CommunicationError',
          result: e
        };
      } else {
        return {
          code: 'CommunicationError',
          result: new Error(`Unknown error ${e}.`)
        };
      }
    }
  }
}
" );
        }
    }
}
