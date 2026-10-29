# CK-Cris

[![Licence](https://img.shields.io/github/license/signature-opensource/CK-Cris.svg)](LICENSE)

The execution side of Cris: the declarations of [CK-Cris-Abstractions](https://github.com/signature-opensource/CK-Cris-Abstractions)
(commands, events, parts and handler attributes) are executed by the packages of this repository.

| Package | Description | Latest stable |
|---------|-------------|---------------|
| [CK.Cris.Executor](CK.Cris.Executor/README.md) | Validating and executing a command, the background host and the event hub. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.Executor.svg?label=CK.Cris.Executor)](https://www.nuget.org/packages/CK.Cris.Executor/) |
| [CK.Cris.BackgroundExecutor](CK.Cris.BackgroundExecutor/README.md) | A DI container dedicated to executing commands away from whatever received them. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.BackgroundExecutor.svg?label=CK.Cris.BackgroundExecutor)](https://www.nuget.org/packages/CK.Cris.BackgroundExecutor/) |
| [CK.Cris.Auth](CK.Cris.Auth/README.md) | The service that enforces the authentication parts, and rebuilds an authentication from them. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.Auth.svg?label=CK.Cris.Auth)](https://www.nuget.org/packages/CK.Cris.Auth/) |
| [CK.Cris.Authentication](CK.Cris.Authentication/README.md) | The CK.AuthenticationInfo counterpart of CK.Cris.Auth (an application uses one or the other), and the contracts between the endpoints and an authentication center. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.Authentication.svg?label=CK.Cris.Authentication)](https://www.nuget.org/packages/CK.Cris.Authentication/) |
| [CK.Testing.Cris.Authentication](CK.Testing.Cris.Authentication/README.md) | Test helpers: a fake actor database, a fake authentication center (its tokens, the operations of the session commands) and the fake login providers of a real one. | [![nuget](https://img.shields.io/nuget/v/CK.Testing.Cris.Authentication.svg?label=CK.Testing.Cris.Authentication)](https://www.nuget.org/packages/CK.Testing.Cris.Authentication/) |
| [CK.Cris.DelayedCommand](CK.Cris.DelayedCommand/README.md) | In-memory delayed execution: a priority queue, a timer, and a handoff. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.DelayedCommand.svg?label=CK.Cris.DelayedCommand)](https://www.nuget.org/packages/CK.Cris.DelayedCommand/) |
| [CK.Cris.HttpSender](CK.Cris.HttpSender/README.md) | An application identity feature that sends commands to a remote party over HTTP. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.HttpSender.svg?label=CK.Cris.HttpSender)](https://www.nuget.org/packages/CK.Cris.HttpSender/) |
| [CK.Cris.Engine](CK.Cris.Engine/README.md) | Setup-time engine (loaded by the setup engine at setup time, never referenced by an application): discovery of every command, event and handler, and the command directory. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.Engine.svg?label=CK.Cris.Engine)](https://www.nuget.org/packages/CK.Cris.Engine/) |
| [CK.Cris.Executor.Engine](CK.Cris.Executor.Engine/README.md) | Setup-time engine (loaded by the setup engine at setup time, never referenced by an application): generation of the executor and receiver bodies. | [![nuget](https://img.shields.io/nuget/v/CK.Cris.Executor.Engine.svg?label=CK.Cris.Executor.Engine)](https://www.nuget.org/packages/CK.Cris.Executor.Engine/) |
