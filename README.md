# CK-Cris

[![Licence](https://img.shields.io/github/license/signature-opensource/CK-Cris.svg)](LICENSE)

The execution side of Cris. The declarations - `ICommand`, `IEvent`, the parts and the handler
attributes - live in
[CK-Cris-Abstractions](https://github.com/signature-opensource/CK-Cris-Abstractions); this repository
is what makes them run.

| Package | Description | Latest stable |
|---------|-------------|---------------|
| [CK.Cris.Executor](CK.Cris.Executor/README.md) | Validating and executing a command, the background host and the event hub. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.Executor.svg?label=CK.Cris.Executor)](https://www.nuget.org/packages/CK.Cris.Executor/) |
| [CK.Cris.BackgroundExecutor](CK.Cris.BackgroundExecutor/README.md) | A DI container dedicated to executing commands away from whatever received them. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.BackgroundExecutor.svg?label=CK.Cris.BackgroundExecutor)](https://www.nuget.org/packages/CK.Cris.BackgroundExecutor/) |
| [CK.Cris.Auth](CK.Cris.Auth/README.md) | The service that enforces the authentication parts, and rebuilds an authentication from them. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.Auth.svg?label=CK.Cris.Auth)](https://www.nuget.org/packages/CK.Cris.Auth/) |
| [CK.Cris.DelayedCommand](CK.Cris.DelayedCommand/README.md) | In-memory delayed execution: a priority queue, a timer, and a handoff. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.DelayedCommand.svg?label=CK.Cris.DelayedCommand)](https://www.nuget.org/packages/CK.Cris.DelayedCommand/) |
| [CK.Cris.HttpSender](CK.Cris.HttpSender/README.md) | An application identity feature that sends commands to a remote party over HTTP. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.HttpSender.svg?label=CK.Cris.HttpSender)](https://www.nuget.org/packages/CK.Cris.HttpSender/) |
| [CK.Cris.Engine](CK.Cris.Engine/README.md) | Setup-time discovery of every command, event and handler, and the command directory. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.Engine.svg?label=CK.Cris.Engine)](https://www.nuget.org/packages/CK.Cris.Engine/) |
| [CK.Cris.Executor.Engine](CK.Cris.Executor.Engine/README.md) | Setup-time generation of the executor and receiver bodies. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.Executor.Engine.svg?label=CK.Cris.Executor.Engine)](https://www.nuget.org/packages/CK.Cris.Executor.Engine/) |

The last two are **engine assemblies**: they are loaded by CKSetup at build time, named by string
rather than referenced, and an application never depends on them. Everything above them is runtime.

`CK.Cris.Executor` is the one to read first: it is where the pipeline lives, and the other six explain
themselves in its terms. Only two of them are built on it, though - `CK.Cris.BackgroundExecutor`
directly, and `CK.Cris.DelayedCommand` through that one, which is why they pair naturally:
a delayed command is submitted into the background container when its date arrives, the same handoff
the executor performs whenever a command has to leave the container that received it.

`CK.Cris.Auth` and `CK.Cris.HttpSender` reference neither. The first is a plain service carrying
handler methods, exactly like application code would - which is the demonstration that the pipeline
has no privileged participants: enforcing an authentication level is an `[IncomingValidator]` method
someone wrote, not a framework feature. The second sends commands *away*, so it needs the
declarations and a transport, not an execution pipeline.
