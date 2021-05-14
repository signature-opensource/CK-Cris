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
            context.PocoCodeGenerator.PocoGenerated += OnPocoGenerated;
            return true;
        }

        void OnPocoGenerated( object? sender, PocoGeneratedEventArgs e )
        {
            if( typeof(ICommand).IsAssignableFrom( e.TypeFile.Type ) )
            {
                var registry = CommandRegistry.Find( e.Monitor, e.TypeFile.Context.CodeContext );
                Debug.Assert( registry != null, "Implement (CSharp code) has necessarily been successfully called." );

                var cmd = registry.Find( e.PocoRootInfo );
                // A IPoco that supports ICommand should be a registered command.
                // If it's not the case, this is weird but that MAY be possible.
                // Defensive programming here.
                if( cmd == null ) return;

                EnsureCrisFiles( e );

                bool isVoidReturn = cmd.ResultType == typeof( void );
                bool isFireAndForget = !isVoidReturn && cmd.ResultType == typeof( NoWaitResult );

                // Compute the (potentially) type name only once by caching the signature.
                var b = e.PocoClassPart;
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
                    .Append( "send: (e: ICrsEndpoint) => e.send( this )" ).NewLine()
                    .CloseBlock();

                foreach( var itf in e.PocoRootInfo.Interfaces )
                {
                    var code = e.TypeFile.File.Body.FindKeyedPart( itf.PocoInterface );
                    Debug.Assert( code != null );
                    code.Append( signature ).Append( ";" ).NewLine();
                }
            }
        }

        static string? AppendCommandModelSignature( ITSCodePart code, CommandRegistry.Entry cmd, PocoGeneratedEventArgs e )
        {
            var signature = "readonly " + (e.TypeFile.Context.Root.PascalCase ? "C" : "c") + "ommandModel: CommandModel<";
            code.Append( signature );
            var typeName = code.AppendAndGetComplexTypeName( e.Monitor, e.Context, cmd.ResultType );
            if( typeName == null ) return null;
            signature += typeName + ">";
            code.Append( ">" );
            return signature;
        }

        private static void EnsureCrisFiles( PocoGeneratedEventArgs e )
        {
            var folder = e.Context.Root.Root.FindOrCreateFolder( "CK" ).FindOrCreateFolder( "Cris" );
            var fModel = folder.FindOrCreateFile( "model.ts", out bool created );
            if( created )
            {
                fModel.Body.Append( @"
export interface CommandModel<TResult> {
    readonly commandName: string;
    readonly isFireAndForget: boolean;
    send: (e: ICrsEndpoint) => Promise<TResult>;
    //applyAmbientValues: (values: { [index: string]: any }) => void;
}

type CommandResult<T> = T extends { commandModel: CommandModel<infer TResult> } ? TResult : never;

export interface ICrsEndpoint {
    send<T>(command: T): Promise<CommandResult<T>>;
}
" );
            }
            e.TypeFile.File.Imports.EnsureImport( "CommandModel", fModel )
                                   .AddType( "ICrsEndpoint" );
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
                    if( cmd.ResultType != typeof(void) && cmd.ResultType != typeof(NoWaitResult) )
                    {
                        g.DeclareTSType( monitor, cmd.ResultType );
                    }
                }

            }
            return true;
        }
    }
}
