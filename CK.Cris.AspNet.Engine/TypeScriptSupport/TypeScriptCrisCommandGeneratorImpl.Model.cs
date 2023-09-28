using CK.Core;
using CK.Cris.AspNet;
using CK.TypeScript.CodeGen;

namespace CK.Setup
{
    public sealed partial class TypeScriptCrisCommandGeneratorImpl
    {
        static void GenerateCrisModelFile( IActivityMonitor monitor, TypeScriptFile<TypeScriptContextRoot> fModel )
        {
            // The import declares the TSTypes for IAspNetCrisResultError and ICrisResult.
            fModel.EnsureImport( monitor, typeof( SimpleUserMessage ) );

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

" );
        }

    }
}
