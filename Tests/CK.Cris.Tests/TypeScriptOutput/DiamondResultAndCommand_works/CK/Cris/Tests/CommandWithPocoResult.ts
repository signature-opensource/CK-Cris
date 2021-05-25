import { CommandModel, ICrsEndpoint } from '../model';
import { IUnifiedResult } from './Result';

export class CommandWithPocoResult implements ICommandWithPocoResult, ICommandWithMorePocoResult, ICommandWithAnotherPocoResult, ICommandUnifiedWithTheResult {
readonly commandModel: CommandModel<IUnifiedResult> =  {
commandName: 'CK.Cris.Tests.ICommandWithPocoResult',
isFireAndForget: false,
send: (e: ICrsEndpoint) => e.send( this )

}
/**
 * Factory method that exposes all the properties as parameters.
 **/
static create(  ) : CommandWithPocoResult

/**
 * Creates a new command and calls a configurator for it.
 * @param config A function that configures the new command.
 **/
static create( config: (c: CommandWithPocoResult) => void ) : CommandWithPocoResult
// Implementation.
static create( config?: (c: CommandWithPocoResult) => void ) : CommandWithPocoResult
 {
const c = new CommandWithPocoResult();
if( config ) config(c);
return c;
}
}
export interface ICommandWithPocoResult {
readonly commandModel: CommandModel<IUnifiedResult>;
}
export interface ICommandWithMorePocoResult extends ICommandWithPocoResult {
readonly commandModel: CommandModel<IUnifiedResult>;
}
export interface ICommandWithAnotherPocoResult extends ICommandWithPocoResult {
readonly commandModel: CommandModel<IUnifiedResult>;
}
export interface ICommandUnifiedWithTheResult extends ICommandWithMorePocoResult, ICommandWithPocoResult, ICommandWithAnotherPocoResult {
readonly commandModel: CommandModel<IUnifiedResult>;
}
