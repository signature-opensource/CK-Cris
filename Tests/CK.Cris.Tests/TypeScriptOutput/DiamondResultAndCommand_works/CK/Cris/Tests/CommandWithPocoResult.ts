import { CommandModel, ICrsEndpoint } from '../model';
import { IUnifiedResult } from './Result';

export class CommandWithPocoResult implements ICommandWithPocoResult, ICommandWithMorePocoResult, ICommandWithAnotherPocoResult, ICommandUnifiedWithTheResult {
// Properties from ICommandWithPocoResult.
// Properties from ICommandWithMorePocoResult.
// Properties from ICommandWithAnotherPocoResult.
// Properties from ICommandUnifiedWithTheResult.
readonly commandModel: CommandModel<
IUnifiedResult
> =  {
commandName: 'CK.Cris.Tests.ICommandWithPocoResult',
isFireAndForget: false,
send: (e: ICrsEndpoint) => e.send( this )

}
}
export interface ICommandWithPocoResult {
readonly commandModel: CommandModel<IUnifiedResult
>;
}
export interface ICommandWithMorePocoResult extends ICommandWithPocoResult {
readonly commandModel: CommandModel<IUnifiedResult
>;
}
export interface ICommandWithAnotherPocoResult extends ICommandWithPocoResult {
readonly commandModel: CommandModel<IUnifiedResult
>;
}
export interface ICommandUnifiedWithTheResult extends ICommandWithMorePocoResult, ICommandWithPocoResult, ICommandWithAnotherPocoResult {
readonly commandModel: CommandModel<IUnifiedResult
>;
}
