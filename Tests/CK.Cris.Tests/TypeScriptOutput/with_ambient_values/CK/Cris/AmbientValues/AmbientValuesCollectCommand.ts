import { CommandModel, ICrsEndpoint } from '../model';
import { IAmbientValues } from './AmbientValues';

export class AmbientValuesCollectCommand implements IAmbientValuesCollectCommand {
readonly commandModel: CommandModel<IAmbientValues> =  {
commandName: 'CK.Cris.AmbientValues.IAmbientValuesCollectCommand',
isFireAndForget: false,
send: (e: ICrsEndpoint) => e.send( this )

}
/**
 * Factory method that exposes all the properties as parameters.
 **/
static create(  ) : AmbientValuesCollectCommand

/**
 * Creates a new command and calls a configurator for it.
 * @param config A function that configures the new command.
 **/
static create( config: (c: AmbientValuesCollectCommand) => void ) : AmbientValuesCollectCommand
// Implementation.
static create( config?: (c: AmbientValuesCollectCommand) => void ) : AmbientValuesCollectCommand
 {
const c = new AmbientValuesCollectCommand();
if( config ) config(c);
return c;
}
}
export interface IAmbientValuesCollectCommand {
readonly commandModel: CommandModel<IAmbientValues>;
}
