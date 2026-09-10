# Background command execution

A DI container dedicated to executing commands away from whatever received them, and two services to
submit into it.

> ℹ️ Read [CK.Cris.Executor](../CK.Cris.Executor/README.md) first: `CrisExecutionHost`, `CrisJob`,
> `ContainerCommandExecutor` and `IExecutingCommand` are its types, and this package assembles them.

## Submit returns immediately, and the handle is the executing command.

```csharp
IExecutingCommand<T> Submit<T>( IActivityMonitor monitor,
                                T command,
                                Action<AmbientServiceHub>? ambientServicesOverride = null,
                                ActivityMonitor.Token? issuerToken = null,
                                IDeferredCommandExecutionContext? deferredExecutionInfo = null,
                                Func<IActivityMonitor, IExecutedCommand, IServiceProvider?, Task>? onExecutedCommand = null,
                                bool? incomingValidationCheck = null )
    where T : class, IAbstractCommand;
```

There are two services, and the difference is not only convenience:

- [`CrisBackgroundExecutorService`](CrisBackgroundExecutorService.cs) is the singleton that does the
  work. Its hub parameter is an `AmbientServiceHub?` you supply, and passing null means "let the
  `[RestoreAmbientServices]` methods build one".
- [`CrisBackgroundExecutor`](CrisBackgroundExecutor.cs) is a scoped service that captures the current
  `AmbientServiceHub` at construction and forwards it. In a request scope this is the one to inject -
  it is what makes the caller's culture and authentication follow the command into the background.

Note the shape of its override parameter, above: an `Action<AmbientServiceHub>`, not a hub. You do not
replace the captured hub, you mutate a `CleanClone()` of it - and only when you pass a non-null
action, so the common call clones nothing. Adjusting one ambient service for one command is therefore
the easy operation, and swapping the whole set is not offered here.

In a scope, the whole thing is six statements:

```csharp
using var scoped = auto.Services.CreateScope();
var poco = scoped.ServiceProvider.GetRequiredService<PocoDirectory>();
var executor = scoped.ServiceProvider.GetRequiredService<CrisBackgroundExecutor>();
var cmd = poco.Create<IMyCommand>( c => c.WantedPower = 3712 );
var ec = executor.Submit( TestHelper.Monitor, cmd ).WithResult<IMyCommandResult>();
var r = await ec.Result;
```

From [`ExecutingCommandTests`](../Tests/CK.Cris.BackgroundExecutor.Tests/ExecutingCommandTests.cs),
whose name for that test says the point: *using the scoped CrisBackgroundExecutor is simple*. Note
what is absent - no hub is passed, because the scoped service already captured it. The sibling test
does the same through `CrisBackgroundExecutorService` and has to write `Submit( monitor, cmd, null )`,
the `null` being "no hub, let the command restore one".

`WithResult<TResult>()` is not a cast: it returns an `IResultAdapter<TResult>` view. Its XML says it
throws an `ArgumentException` when the command is not an `ICommand<TResult>` - **read the body
instead**. The only guard is `ResultType == typeof( void )`, so it rejects a result-less `ICommand`
and never compares `TResult` with the command's actual result type. Ask for the wrong `TResult` and
you get an `InvalidCastException` later, out of the `Result` task;
`ExecutingCommandTests.ExecutingCommand_handles_different_Result_type_Async` asserts exactly that.

The adapter also changes the failure mode: its `Result` task completes with an exception when the
command result is an `ICrisResultError`, so `await ec.Result` throws where awaiting the plain
`ExecutedCommand` would have handed you the error object. With one exception to the exception -
`OnRequestCompletion` tests `r is TResult` **before** `r is ICrisResultError`, so a `TResult` that an
`ICrisResultError` is assignable to receives the error as a *success*.

The returned `IExecutingCommand<T>` is the whole tracking surface: await its executed-command task for
the result, or watch its live `ImmediateEvents` collection while it runs.

`issuerToken` is the correlation between the two contexts. Left null, one is taken from the monitor,
so the background logs are linkable to the caller's without doing anything.

## The container is the point, not the thread.

[`CrisBackgroundDIContainerDefinition`](CrisBackgroundDIContainerDefinition.cs) declares a backend DI
container, and `CrisBackgroundExecutorService` is a `ContainerCommandExecutor<...Data>` over it. So
"background" here means *another container*, and the parallel runners come from the shared
`CrisExecutionHost` rather than from anything defined here.

That is what makes `PrepareJobAsync` the interesting override:

> Either creates the `AsyncServiceScope` if the job has an `AmbientServiceHub`, or restores an
> `AmbientServiceHub` from the command before creating the `AsyncServiceScope`.

Two paths into one scope. The first is the caller handing over its ambient state; the second is the
command reconstructing it from its own `[RestoreAmbientServices]` methods - which is why a command
that carries ambient values can be submitted from a context that has none, such as a timer or a queue
reader.

Failure to restore is not an exception: the scope is not created, `IExecutedCommand.Result` is an
`ICrisResultError`, and the `onExecutedCommand` callback is still called with a **null** service
provider. That null is the signal, and it is the documented way to tell "the command failed" from "the
command never got a container".

## Requires.

- `CK.Cris.Executor`, for `CrisExecutionHost`, `CrisJob`, `ContainerCommandExecutor<T>` and
  `IExecutingCommand<T>`.
