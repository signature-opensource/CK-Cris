Executes Cris commands in a dedicated background DI container (`CrisBackgroundDIContainerDefinition`), away from the endpoint that received
them. `CrisBackgroundExecutorService` (singleton) and `CrisBackgroundExecutor` (scoped: it captures the caller's `AmbientServiceHub`) submit
a command and immediately return an `IExecutingCommand`. The ambient services (culture, authentication...) follow the command, or are
restored from it, in the background container.
