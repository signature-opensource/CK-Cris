using CK.Core;
using CK.Cris.AmbientValues;
using CK.Cris.AspNet;
using CK.TypeScript.CodeGen;
using System;

namespace CK.Setup
{
    public sealed partial class TypeScriptCrisCommandGeneratorImpl
    {
        static void GenerateCrisHttpEndpoint( IActivityMonitor monitor, TypeScriptFile<TypeScriptContext> fHttpEndpoint )
        {
            // Importing the Model (ICommand<T>, CommandModel, ICrisEndpoint, etc.).
            fHttpEndpoint.Imports.Append( "import {ICrisEndpoint,ICommand,ExecutedCommand,CrisError} from './Model';'" ).NewLine();

            // The import declares the TSTypes for IAspNetCrisResultError and ICrisResult.
            fHttpEndpoint.EnsureImport( monitor, typeof( UserMessageLevel ),
                                                 typeof( SimpleUserMessage ),
                                                 typeof( CrisAspNetService.IAspNetCrisResultError ),
                                                 typeof( CrisAspNetService.IAspNetCrisResult ),
                                                 typeof( IAmbientValuesCollectCommand ) );

            fHttpEndpoint.Imports.EnsureImportFromLibrary( new LibraryImport( "axios", "^1.2.3", DependencyKind.Dependency ),
                                                                       "AxiosInstance", "AxiosHeaders", "RawAxiosRequestConfig" );
            fHttpEndpoint.Body.Append( @"
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
}" );
        }



    }
}
