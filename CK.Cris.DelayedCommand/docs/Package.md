An in-memory implementation of the CK.IO.DelayedCommand `IDelayedCommand`: `CrisDelayedCommandService` keeps the commands in a priority
queue with a timer, submits each one to the background executor when its date arrives, and then raises an `IDelayedCommandExecutedEvent`.
Nothing survives a restart: `StoreAsync`, `OnCommandExecuting` and the command handler are virtual so that a persistent implementation can
be built on it. `DelayedCommandEntry` tracks a waiting command.
