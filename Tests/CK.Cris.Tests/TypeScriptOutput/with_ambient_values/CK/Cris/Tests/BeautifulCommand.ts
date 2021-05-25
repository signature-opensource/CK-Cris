import { CommandModel, ICrsEndpoint } from '../model';

export class BeautifulCommand implements IBeautifulCommand {
readonly commandModel: CommandModel<void> =  {
commandName: 'CK.Cris.Tests.TypeScriptGenerationTests+IBeautifulCommand',
isFireAndForget: false,
send: (e: ICrsEndpoint) => e.send( this )

}
beauty: string;
color: string;
/**
 * Factory method that exposes all the properties as parameters.
 **/
static create( beauty: string,color: string ) : BeautifulCommand

/**
 * Creates a new command and calls a configurator for it.
 * @param config A function that configures the new command.
 **/
static create( config: (c: BeautifulCommand) => void ) : BeautifulCommand
// Implementation.
static create( beauty: string|((c:BeautifulCommand) => void),color?: string ) {
const c = new BeautifulCommand();
if( typeof beauty === 'function' ) beauty(c);
else {
c.beauty = beauty;
c.color = color;
return c;
}

}
}
export interface IBeautifulCommand {
beauty: string;
readonly commandModel: CommandModel<void>;
}
