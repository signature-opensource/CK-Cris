using CK.Core;
using CK.Cris.AspNet;
using CK.TypeScript.CodeGen;
using System.Diagnostics.CodeAnalysis;

namespace CK.Setup
{
    public sealed partial class TypeScriptCrisCommandGeneratorImpl
    {
        [MemberNotNull( nameof( _command ), nameof( _abstractCommand ), nameof( _crisPoco ) )]
        TypeScriptFile EnsureCrisCommandModel( IActivityMonitor monitor, TypeScriptContext context )
        {
            if( _modelFile == null )
            {
                _modelFile = context.Root.Root.FindOrCreateTypeScriptFile( "CK/Cris/Model.ts" );
                GenerateCrisModelFile( monitor, context, _modelFile );
                _crisPoco = new TSBasicType( context.Root.TSTypes, "ICrisPoco", imports => imports.EnsureImport( _modelFile, "ICrisPoco" ), null );
                _abstractCommand = new TSBasicType( context.Root.TSTypes, "IAbstractCommand", imports => imports.EnsureImport( _modelFile, "IAbstractCommand" ), null );
                _command = new TSBasicType( context.Root.TSTypes, "ICommand", imports => imports.EnsureImport( _modelFile, "ICommand" ), null );
            }
            Throw.DebugAssert( _command != null && _abstractCommand != null && _crisPoco != null );
            return _modelFile;

            static void GenerateCrisModelFile( IActivityMonitor monitor, TypeScriptContext context, TypeScriptFile fModel )
            {
                fModel.Imports.EnsureImport( monitor, typeof( SimpleUserMessage ) );
                fModel.Imports.EnsureImport( monitor, typeof( UserMessageLevel ) );
                var pocoType = context.Root.TSTypes.ResolveTSType( monitor, typeof( IPoco ) );
                // Imports the IPoco itself...
                pocoType.EnsureRequiredImports( fModel.Imports );

                fModel.Body.Append( """
                                /**
                                 * Describes a Command type. 
                                 **/
                                export interface ICommandModel {
                                    /**
                                     * This supports the CrisEndpoint implementation. This is not to be used directly.
                                     **/
                                    readonly applyAmbientValues: (command: any, a: any, o: any ) => void;
                                }

                                /** 
                                 * Abstraction of any Cris objects (currently only commands).
                                 **/
                                export interface ICrisPoco extends IPoco
                                {
                                    readonly _brand: IPoco["_brand"] & {"ICrisPoco": any};
                                }

                                /** 
                                 * Command abstraction.
                                 **/
                                export interface IAbstractCommand extends ICrisPoco
                                {
                                    /** 
                                     * Gets the command model.
                                     **/
                                    get commandModel(): ICommandModel;

                                    readonly _brand: ICrisPoco["_brand"] & {"ICommand": any};
                                }

                                /** 
                                 * Command with or without a result.
                                 * The C# ICommand (without result) is the TypeScript ICommand<void>.
                                 **/
                                export interface ICommand<out TResult = void> extends IAbstractCommand {
                                    readonly _brand: IAbstractCommand["_brand"] & {"ICommandResult": void extends TResult ? any : TResult};
                                }
                                                                
                                
                                /** 
                                 * Captures the result of a command execution.
                                 **/
                                export type ExecutedCommand<T> = {
                                    /** The executed command. **/
                                    readonly command: ICommand<T>,
                                    /** The execution result. **/
                                    readonly result: CrisError | T,
                                    /**
                                     * An optional list of UserMessageLevel.info, UserMessageLevel.warn or UserMessageLevel.error
                                     * messages issued by the validation of the command: there can be info or warn messages even if the 
                                     * command has been succesfully executed. 
                                     * Validation error messages also appear in the CrisError.messages.
                                     **/
                                    readonly validationMessages?: Array<SimpleUserMessage>;
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
                                    public readonly errorType : "CommunicationError"|"ValidationError"|"ExecutionError";
                                    /**
                                     * Gets the errors. At least one error is guaranteed to exist.
                                     */
                                    public readonly errors: ReadonlyArray<string>; 
                                    /**
                                     * Gets the validationMessages if any.
                                     */
                                    public readonly validationMessages?: ReadonlyArray<SimpleUserMessage>; 
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
                                                 isValidationError: boolean,
                                                 errors: ReadonlyArray<string>, 
                                                 innerError?: Error,
                                                 validationMessages?: ReadonlyArray<SimpleUserMessage>,
                                                 logKey?: string ) 
                                    {
                                        super( errors[0] );
                                        this.command = command;   
                                        this.errorType = isValidationError 
                                                            ? "ValidationError" 
                                                            : innerError ? "CommunicationError" : "ExecutionError";
                                        this.innerError = innerError;
                                        this.errors = errors;
                                        this.validationMessages = validationMessages;
                                        this.logKey = logKey;
                                    }
                                }
                                
                                """ );
            }
        }

    }
}
