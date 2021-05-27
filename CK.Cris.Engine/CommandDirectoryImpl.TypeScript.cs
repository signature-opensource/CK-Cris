using CK.CodeGen;
using CK.Core;
using CK.Cris;
using CK.StObj.TypeScript;
using CK.StObj.TypeScript.Engine;
using CK.TypeScript.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CK.Setup.Cris
{
    public partial class CommandDirectoryImpl : ITSCodeGenerator
    {
        bool ITSCodeGenerator.ConfigureTypeScriptAttribute( IActivityMonitor monitor, ITSTypeFileBuilder builder, TypeScriptAttribute a )
        {
            // Nothing to do: we don't want to interfere with the standard IPoco handling.
            return true;
        }
        bool ITSCodeGenerator.Initialize( IActivityMonitor monitor, TypeScriptContext context )
        {
            context.PocoCodeGenerator.PocoGenerating += OnPocoGenerating;
            return true;
        }

        bool ITSCodeGenerator.GenerateCode( IActivityMonitor monitor, TypeScriptContext g )
        {
            var registry = CommandRegistry.Find( monitor, g.CodeContext );
            Debug.Assert( registry != null, "Implement (CSharp code) has necessarily been successfully called." );
            using( monitor.OpenInfo( $"Declaring TypeScript support for {registry.Commands.Count} commands." ) )
            {
                foreach( var cmd in registry.Commands )
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


        // We capture the list of the parameters that corresponds to the Ambient Values.
        List<TypeScriptVarType>? _ambientValuesParameters;

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

                Debug.Assert( _ambientValuesParameters != null, "The PocoGeneratingEventArgs for IAmbientValues has been called." );

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
                        .Append( "send: (e: ICrsEndpoint) => e.send( this )" ).Append( "," ).NewLine()
                        .Append( "applyAmbientValues: (values: { [index: string]: any }, force?: boolean ) => " )
                        .OpenBlock()
                            .Append( ApplyAmbientValues )
                        .CloseBlock()
                    .CloseBlock( withSemiColon: true );

                // All the interfaces share the commandModel signature. 
                foreach( var itf in e.PocoClass.PocoRootInfo.Interfaces )
                {
                    var code = e.TypeFile.File.Body.FindKeyedPart( itf.PocoInterface );
                    Debug.Assert( code != null );
                    code.Append( signature ).Append( ";" ).NewLine();
                }

                void ApplyAmbientValues( ITSCodePart b )
                {
                    Debug.Assert( _ambientValuesParameters != null );
                    bool atLeastOne = false;
                    foreach( var a in _ambientValuesParameters )
                    {
                        // Find the property with the same (parameter) name.
                        var fromAmbient = e.PocoClass.Properties.FirstOrDefault( p => p.ParameterName == a.Name );
                        if( fromAmbient != null )
                        {
                            // Documents it.
                            fromAmbient.Property.Comment += Environment.NewLine + "(This is an Ambient Value.)";
                            e.Monitor.Debug( $"Property '{fromAmbient.Property.Name}' is an ambient value." );
                            // The parameter in the create method SHOULD exist. We remove it.
                            int idx = e.PocoClass.CreateParameters.IndexOf( p => p.Name == fromAmbient.ParameterName );
                            if( idx >= 0 ) e.PocoClass.CreateParameters.RemoveAt( idx );
                            // Adds the assignment: this property comes from its ambient value.
                            if( atLeastOne ) b.NewLine();
                            b.Append( "if( force || typeof this." ).Append( fromAmbient.Property.Name ).Append( " === \"undefined\" ) this." ).Append( fromAmbient.Property.Name )
                                .Append( " = values[" ).AppendSourceString( fromAmbient.ParameterName ).Append( "];" );
                            atLeastOne = true;
                        }
                    }
                    if( !atLeastOne && e.PocoClass.PocoRootInfo != _registry.AmbientValues ) b.Append( "// This command has no property that appear in the Ambient Values." ).NewLine();
                }
            }
            else if( e.PocoClass.PocoRootInfo == _registry.AmbientValues )
            {
                Debug.Assert( _ambientValuesParameters == null );
                _ambientValuesParameters = e.PocoClass.CreateParameters;
            }
        }

        static string? AppendCommandModelSignature( ITSCodePart code, CommandRegistry.Entry cmd, PocoGeneratingEventArgs e )
        {
            var signature = "readonly " + (e.TypeFile.Context.Root.PascalCase ? "C" : "c") + "ommandModel: CommandModel<";
            code.Append( signature );
            var typeName = code.AppendAndGetComplexTypeName( e.Monitor, e.TypeFile.Context, cmd.ResultType );
            if( typeName == null ) return null;
            signature += typeName + ">";
            code.Append( ">" );
            return signature;
        }

        static void EnsureCrisModel( PocoGeneratingEventArgs e )
        {
            var folder = e.TypeFile.Context.Root.Root.FindOrCreateFolder( "CK" ).FindOrCreateFolder( "Cris" );
            var fModel = folder.FindOrCreateFile( "Model.ts", out bool created );
            if( created )
            {
                fModel.Body.Append( @"
export interface CommandModel<TResult> {
    readonly commandName: string;
    readonly isFireAndForget: boolean;
    send: (e: ICrsEndpoint) => Promise<TResult>;
    applyAmbientValues: (values: { [index: string]: any }, force?: boolean ) => void;
}

type CommandResult<T> = T extends { commandModel: CommandModel<infer TResult> } ? TResult : never;

export interface ICrsEndpoint {
    send<T>(command: T): Promise<CommandResult<T>>;
}
" );
            }
            e.TypeFile.File.Imports.EnsureImport( fModel, "CommandModel", "ICrsEndpoint" );
        }

    }
}
