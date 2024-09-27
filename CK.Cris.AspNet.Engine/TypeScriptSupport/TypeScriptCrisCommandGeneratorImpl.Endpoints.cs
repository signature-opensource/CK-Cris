using CK.Core;
using CK.Cris.AmbientValues;
using CK.Cris.AspNet;
using CK.TypeScript.CodeGen;
using System;
using System.Threading;

namespace CK.Setup
{
    public sealed partial class TypeScriptCrisCommandGeneratorImpl
    {
        void OnAfterCodeGeneration( object? sender, EventMonitoredArgs e )
        {
            Throw.DebugAssert( sender is TypeScriptContext );
            // If model has not been created, skip everything.
            if( _modelFile != null )
            {
                TypeScriptContext context = (TypeScriptContext)sender;
                var crisEndpointFile = GenerateCrisEndpoint( e.Monitor, context, _modelFile );
                // If there is no Json serialization, we skip the HttpEndpoint as it uses the CTSType.
                if( context.PocoCodeGenerator.CTSTypeSystem != null )
                {
                    GenerateCrisHttpEndpoint( e.Monitor, _modelFile, crisEndpointFile, context.PocoCodeGenerator.CTSTypeSystem.CTSType );
                }
            }
        }

        static TypeScriptFile GenerateCrisEndpoint( IActivityMonitor monitor, TypeScriptContext context, TypeScriptFile modelFile )
        {
            TypeScriptFile fEndpoint = modelFile.Folder.FindOrCreateTypeScriptFile( "CrisEndpoint.ts" );

            // AmbientValuesOverride is in the same folder as AmbienValues.ts.
            var ambientValuesOverride = context.Root.TSTypes.FindByTypeName( "AmbientValuesOverride" );
            Throw.CheckState( "AmbientValuesOverride is automatically created in the same folder as AmbientValues.ts and IAmbientValues is a registered type.",
                              ambientValuesOverride != null );
            // Importing:
            // - the Model objects ICommand, ExecutedCommand and CrisError.
            // - The IAmbientValues and IAmbientValuesCollectCommand.
            // - The AmbientValuesOverride.
            fEndpoint.Imports.EnsureImport( modelFile, "ICommand", "ExecutedCommand", "CrisError" )
                             .EnsureImport( monitor, typeof( IAmbientValues ),
                                                     typeof( IAmbientValuesCollectCommand ) )
                             .EnsureImport( ambientValuesOverride );

            fEndpoint.Body.Append( """
                            /**
                            * Abstract Cris endpoint. 
                            * The doSendAsync protected method must be implemented.
                            */
                            export abstract class CrisEndpoint
                            {
                                private _ambientValuesRequest: Promise<AmbientValues>|undefined;
                                private _ambientValues: AmbientValues|undefined;
                                private _subscribers: Set<( eventSource: CrisEndpoint ) => void>;
                                private _isConnected: boolean;

                                constructor()
                                {
                                    this.ambientValuesOverride = new AmbientValuesOverride();
                                    this._isConnected = false;
                                    this._subscribers = new Set<() => void>();
                                }

                                /**
                                * Enables ambient values to be overridden.
                                * Sensible ambient values (like the actorId when CK.IO.Auth is used) are checked against
                                * secured contextual values: overriding them will trigger a ValidationError. 
                                **/    
                                public readonly ambientValuesOverride: AmbientValuesOverride;


                                //#region isConnected
                                /** 
                                * Gets whether this HttpEndpointService is connected: the last command sent
                                * has been handled by the server. 
                                **/
                                public get isConnected(): boolean { return this._isConnected; }

                                /**
                                * Registers a callback function that will be called when isConnected changed.
                                * @param func A callback function.
                                */
                                public addOnIsConnectedChanged( func: ( eventSource: CrisEndpoint ) => void ): void 
                                {
                                    if( func ) this._subscribers.add( func );
                                }

                                /**
                                * Unregister a previously registered callback.
                                * @param func The callback function to remove.
                                * @returns True if the callback has been found and removed, false otherwise.
                                */
                                public removeOnIsConnectedChange( func: ( eventSource: CrisEndpoint ) => void ): boolean {
                                    return this._subscribers.delete( func );
                                }

                                /**
                                * Sets whether this endpoint is connected or not. When setting false, this triggers
                                * an update of the endpoint values that will run until success and eventually set
                                * a true isConnected back.
                                * @param value Whether the connection mus be considered available or not.
                                */
                                protected setIsConnected( value: boolean ): void 
                                {
                                    if( this._isConnected !== value )
                                    {
                                        this._isConnected = value;
                                        if( !value ) 
                                        {
                                            this.updateAmbientValuesAsync();
                                        }
                                        this._subscribers.forEach( func => func( this ) );
                                    }
                                }

                                //#endregion

                                /**
                                * Sends a AmbientValuesCollectCommand and waits for its return.
                                * Next commands will wait for the ubiquitous values to be received before being sent.
                                **/    
                                public updateAmbientValuesAsync() : Promise<AmbientValues>
                                {
                                    if( this._ambientValuesRequest ) return this._ambientValuesRequest;
                                    this._ambientValues = undefined;
                                    return this._ambientValuesRequest = this.waitForAmbientValuesAsync();
                                }

                                /**
                                * Sends a command and returns an ExecutedCommand with the command's result or a CrisError.
                                **/    
                                public async sendAsync<T>(command: ICommand<T>): Promise<ExecutedCommand<T>>
                                {
                                    let a = this._ambientValues;
                                    // Don't use coalesce here since there may be no ambient values (an empty object is truthy).
                                    if( a === undefined ) a = await this.updateAmbientValuesAsync();
                                    command.commandModel.applyAmbientValues( command, a, this.ambientValuesOverride );
                                    return await this.doSendAsync( command ); 
                                }

                                /**
                                * Sends a command and returns the command's result or throws a CrisError.
                                **/    
                                public async sendOrThrowAsync<T>( command: ICommand<T> ): Promise<T>
                                {
                                    const r = await this.sendAsync( command );
                                    if( r.result instanceof CrisError ) throw r.result;
                                    return r.result;
                                }

                                /**
                                * Core method to implement. Can use the handleJsonResponse helper to create 
                                * the final ExecutedCommand<T> from a Json object response. 
                                * @param command The command to send.
                                * @returns The resulting ExecutedCommand<T>.
                                */
                                protected abstract doSendAsync<T>(command: ICommand<T>): Promise<ExecutedCommand<T>>;

                                private async waitForAmbientValuesAsync() : Promise<AmbientValues>
                                {
                                    while(true)
                                    {
                                        var e = await this.doSendAsync( new AmbientValuesCollectCommand() );
                                        if( e.result instanceof CrisError )
                                        {
                                            console.error( "Error while getting AmbientValues. Retrying.", e.result );
                                            this.setIsConnected( false );
                                        }
                                        else
                                        {
                                            this._ambientValuesRequest = undefined;
                                            this._ambientValues = <AmbientValues>e.result;
                                            this.setIsConnected( true );
                                            return this._ambientValues;
                                        }
                                    }
                                }
                            }
                            """ );
            return fEndpoint;
        }


        static void GenerateCrisHttpEndpoint( IActivityMonitor monitor,
                                              TypeScriptFile modelFile,
                                              TypeScriptFile fEndpoint,
                                              ITSType ctsType )
        {
            TypeScriptFile fHttpEndpoint = modelFile.Folder.FindOrCreateTypeScriptFile( "HttpCrisEndpoint.ts" );
            // Importing:
            // - the Model objects ICommand, ExecutedCommand and CrisError.
            // - The base CrisEndPoint.
            // - The IAspNetCrisResult server result model.
            // - Axios to send/receive the POST.
            // - The CTSType to serialize/deserialize.
            // - The IAspNetCrisResultError that must be transformed into a CrisError.

            // The AfterCodeGeneration detect monitor error or fatal.
            var axios = modelFile.Root.LibraryManager.RegisterLibrary( monitor, "axios", "^1.5.1", DependencyKind.Dependency );
            if( axios == null ) return;

            fHttpEndpoint.Imports.EnsureImport( modelFile, "ICommand", "ExecutedCommand", "CrisError" )
                                 .EnsureImport( fEndpoint, "CrisEndpoint" )
                                 .EnsureImport( monitor, typeof( IAspNetCrisResult ) )
                                 .EnsureImportFromLibrary( axios, "AxiosInstance", "AxiosHeaders", "RawAxiosRequestConfig" )
                                 .EnsureImport( ctsType )
                                 .EnsureImport( monitor, typeof( IAspNetCrisResultError ) );

            fHttpEndpoint.Body.Append( """
                                       const defaultCrisAxiosConfig: RawAxiosRequestConfig = {
                                           responseType: 'text',
                                           headers: {
                                             common: new AxiosHeaders({
                                               'Content-Type': 'application/json'
                                             })
                                           }
                                       };

                                       /**
                                        * Http Cris Command endpoint. 
                                        **/
                                       export class HttpCrisEndpoint extends CrisEndpoint
                                       {
                                           /**
                                            * Replaceable axios configuration.
                                            **/
                                           public axiosConfig: RawAxiosRequestConfig; 

                                           /**
                                            * Initializes a new HttpEndpoint that uses an Axios instance bound to a endpoint url.  
                                            * @param axios The axios instance.
                                            * @param crisEndpointUrl The Cris endpoint url to use.
                                            **/
                                           constructor(private readonly axios: AxiosInstance, private readonly crisEndpointUrl: string)
                                           {
                                               super();
                                               this.axiosConfig = defaultCrisAxiosConfig;
                                           }

                                           protected override async doSendAsync<T>(command: ICommand<T>): Promise<ExecutedCommand<T>>
                                           {
                                              try
                                              {
                                                  const req = JSON.stringify(CTSType.toTypedJson(command));
                                                  const resp = await this.axios.post(this.crisEndpointUrl, req, this.axiosConfig);
                                                  const netResult = <AspNetResult>CTSType["AspNetResult"].nosj( JSON.parse(resp.data) );
                                                  let r = netResult.result;
                                                  if( r instanceof AspNetCrisResultError ) 
                                                  {
                                                      r = new CrisError(command, r.isValidationError, r.errors, undefined, netResult.validationMessages, r.logKey);
                                                  }
                                                  return {command: command, result: <T|CrisError>r, validationMessages: netResult.validationMessages, correlationId: netResult.correlationId };
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
                                                  this.setIsConnected(false);
                                                  return {command, result: new CrisError(command, false, ["Communication error"], error )};                                              }
                                           }
                                       }
                                       """ );
        }



    }
}
