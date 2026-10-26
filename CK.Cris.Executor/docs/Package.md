The runtime side of Cris: validating an incoming command, then executing it. `RawCrisReceiver` runs the incoming validators and the ambient
services configurators, `RawCrisExecutor` runs the handling validators, the handler and the post handlers (their bodies are generated).
`CrisExecutionContext` is what a handler uses to execute commands and emit events, `CrisExecutionHost` runs `CrisJob`s on parallel runners
for a `ContainerCommandExecutor`, and `CrisEventHub` exposes the routed events.
