
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
