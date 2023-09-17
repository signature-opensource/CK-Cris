using CK.CodeGen;
using CK.Core;
using CK.Cris;
using CK.Cris.AspNet;
using CK.Setup.Cris;
using CK.StObj.TypeScript;
using CK.StObj.TypeScript.Engine;
using CK.TypeScript.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup
{
    public sealed class TypeScriptCrisCommandGeneratorImpl : ITSCodeGenerator
    {
        CrisRegistry? _registry;

        bool ITSCodeGenerator.ConfigureTypeScriptAttribute( IActivityMonitor monitor, ITSTypeFileBuilder builder, TypeScriptAttribute a )
        {
            // Nothing to do: we don't want to interfere with the standard IPoco handling.
            return true;
        }
        bool ITSCodeGenerator.Initialize( IActivityMonitor monitor, TypeScriptContext context )
        {
            _registry = CrisRegistry.Find( monitor, context.CodeContext );
            Throw.DebugAssert( _registry != null, "CSharp code implementation has necessarily been successfully called." );
            context.PocoCodeGenerator.PocoGenerating += OnPocoGenerating;
            return true;
        }

        bool ITSCodeGenerator.GenerateCode( IActivityMonitor monitor, TypeScriptContext g )
        {
            // Nothing to do here. We just react to the OnPocoGenerating event raised by the
            // Poco generator and handles ICrisPoco that MUST be declared as a TypeScript type
            // (either by the TSTypeScriptAttribute ot by the configuration).
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
        /// of the sendAsync method.
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
            Throw.DebugAssert( _registry != null, "CS code generation ran. Implement (CSharp code) has necessarily been successfully called." );

            // The poco that interest us are the ICrisPoco and the IAmbientValues that we need:
            // the first ICrisPoco declares the IAmbientValues and when the IAmbientValues poco
            // is registered, then the IAspNetCrisResult and IAspNetCrisResultError are also declared.
            if( typeof(ICrisPoco).IsAssignableFrom( e.TypeFile.Type ) )
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

                Throw.DebugAssert( _ambientValuesProperties != null, "The PocoGeneratingEventArgs for IAmbientValues has been called." );

                EnsureCrisModel( e );

                // Declares the command result, whatever it is.
                // If it's a IPoco, it will benefit from the same treatment as the command above.
                // Some types are handled by default, but if there is eventually no generator for the
                // type then the setup fails.
                if( cmd.ResultType != typeof( void ) )
                {
                    e.TypeFile.Context.DeclareTSType( e.Monitor, cmd.ResultType );
                }

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
                        .Append( "commandName: " ).AppendSourceString( cmd.PocoName ).Append( "," ).NewLine()
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
                        Throw.DebugAssert( code != null );
                        code.Append( signature ).Append( ";" ).NewLine();
                    }
                }

                void ApplyAmbientValues( ITSCodePart b )
                {
                    Throw.DebugAssert( _ambientValuesProperties != null );
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
                Throw.DebugAssert( _ambientValuesProperties == null );
                _ambientValuesProperties = e.PocoClass.Properties;
            }
        }

        static string? AppendCommandModelSignature( ITSCodePart code, CrisRegistry.Entry cmd, PocoGeneratingEventArgs e )
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
            // The import declares the TSTypes for IAspNetCrisResultError and ICrisResult.
            fModel.EnsureImport( monitor, typeof( UserMessageLevel ), typeof( SimpleUserMessage ), typeof( IAspNetCrisResultError ), typeof( IAspNetCrisResult ) );

            fModel.Imports.EnsureImportFromLibrary( new LibraryImport( "axios", "^1.2.3", DependencyKind.Dependency ),
                                                                       "AxiosInstance", "AxiosHeaders", "RawAxiosRequestConfig" );
            fModel.Body.Append( @"

/**
 * Describes a command. 
 **/
export type CommandModel<TResult> = {
    /**
     * Gets the name of the command. 
     **/
    readonly commandName: string;
    /**
     * Configures any ambient values that the command holds. 
     **/
    readonly applyAmbientValues: (values: { [index: string]: any }, force?: boolean ) => void;
}

/** 
 * Command abstraction: command with or without a result. 
 * **/
export interface ICommand<TResult = void> { 
    /**
     * Gets the command description. 
     **/
    readonly commandModel: CommandModel<TResult>;
}

/** 
 * Captures the result of a command execution.
 **/
export type ExecutedCommand<T> = {
    /** The executed command. **/
    readonly command: ICommand<T>,
    /** The execution result. **/
    readonly result: CrisError | T,
    /** Optional correlation identifier. **/
    readonly correlationId?: string
};

/**
 * Captures communication, validation or execution error.
 **/
export class CrisError extends Error {
    /**
     * Get this error type.
     */
    public readonly errorType : ""CommunicationError""|""ValidationError""|""ExecutionError"";
    /**
     * Gets the messages. At least one message is guranteed to exist.
     */
    public readonly messages: ReadonlyArray<SimpleUserMessage>; 
    /**
     * The Error.cause support is a mess. This replaces it at this level. 
     */
    public readonly innerError?: Error; 
    /**
     * When defined, enables to find the backend log entry.
     */
    public readonly logKey?: string; 
    /**
     * Gets the command that failed.
     */
    public readonly command: ICommand<unknown>;

    constructor( command: ICommand<unknown>, 
                 message: string, 
                 isValidationError: boolean,
                 innerError?: Error, 
                 messages?: ReadonlyArray<SimpleUserMessage>,
                 logKey?: string ) 
    {
        super( message );
        this.command = command;   
        this.errorType = isValidationError 
                            ? ""ValidationError"" 
                            : innerError ? ""CommunicationError"" : ""ExecutionError"";
        this.innerError = innerError;
        this.messages = messages && messages.length > 0 
                        ? messages
                        : [new SimpleUserMessage(UserMessageLevel.Error,message,0)];
        this.logKey = logKey;
    }
}

export interface ICrisEndpoint {
  sendAsync<T>(command: ICommand<T>): Promise<ExecutedCommand<T>>;
  sendOrThrowAsync<T>( command: ICommand<T> ): Promise<T>;
}

const defaultCrisAxiosConfig: RawAxiosRequestConfig = {
  responseType: 'text',
  headers: {
    common: new AxiosHeaders({
      'Content-Type': 'application/json'
    })
  }
};

export class HttpCrisEndpoint implements ICrisEndpoint
{
    public axiosConfig: RawAxiosRequestConfig; // Allow user replace

    constructor(private readonly axios: AxiosInstance, private readonly crisEndpointUrl: string)
    {
        this.axiosConfig = defaultCrisAxiosConfig;
    }

    public async sendAsync<T>(command: ICommand<T>): Promise<ExecutedCommand<T>>
    {
        try
        {
          let string = `[""${command.commandModel.commandName}""`;
          string += `,${JSON.stringify(command, (key, value) => {
            return key == ""commandModel"" ? undefined : value;
          })}]`;
          const resp = await this.axios.post<string>(this.crisEndpointUrl, string, this.axiosConfig);

          return fromData( command, JSON.parse(resp.data) );
        }
        catch( e )
        {
            var error : Error;
            if( e instanceof Error)
            {
                error = e;
            }
            else
            {
                // Error.cause is a mess. Log it.
                console.error( e );
                error = new Error(`Unhandled error ${e}.`);
            }
            return {command, result: new CrisError(command,""Communication error"",false, error )};
        }

        function fromData<T>( cmd: ICommand<T>, data: any ) : ExecutedCommand<T>
        {
            if( typeof data.correlationId !== ""undefined"" && data.result instanceof Array && data.result.length == 2 )
            {
                if( data.result[0] === ""AspNetCrisResultError"" )
                {
                    // Normalized null or empty to undefined.
                    data.correlationId = data.correlationId ? data.correlationId : undefined;
                    const e = data.result[1] as {isValidationError?: boolean, logKey?: string, messages?: ReadonlyArray<[UserMessageLevel,string,number]>};
                    if( typeof e.isValidationError === ""boolean"" && e.messages instanceof Array )
                    {
                        const messages: Array<SimpleUserMessage> = [];
                        for( const msg of e.messages )
                        {
                            if(!(msg instanceof Array && msg.length == 3)) return invalidResponse(cmd,e.logKey);
                            // silently skip potential [0] or other incomplete arrays.
                            if(msg.length == 3)
                            {
                                messages.push( new SimpleUserMessage(msg[0],msg[1],msg[2]) );
                            }
                        }
                        const m = messages.find( m => m.level === UserMessageLevel.Error ) 
                                    ?? messages.find( m => m.level === UserMessageLevel.Warn )
                                    ?? messages.find( m => m.level === UserMessageLevel.Info );
                        const message = m && m.message ? m.message : 'Error (missing Cris error message)';
                        return {command: cmd, result: new CrisError(cmd,message,e.isValidationError,undefined,messages,e.logKey), correlationId: data.correlationId };
                    }
                    return invalidResponse( cmd, data.correlationId );
                }
                return {command: cmd, result: data.result[1] as T, correlationId: data.correlationId };
            }
            return invalidResponse( cmd );

            function invalidResponse( cmd: ICommand<unknown>, cId?: string ) 
            {
                const m = 'Invalid command response.';
                return {command: cmd, result: new CrisError(cmd, m, false, new Error(m)), correlationId: cId};
            } 
        }
    }

    public async sendOrThrowAsync<T>( command: ICommand<T> ): Promise<T>
    {
        const r = await this.sendAsync( command );
        if( r.result instanceof Error ) throw r.result;
        return r.result;
    }
}
" );
        }

    }
}
