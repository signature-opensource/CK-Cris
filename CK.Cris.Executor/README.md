# Executing commands

The runtime side of Cris: two abstract singletons whose bodies are generated, an execution context
that runs a command and the commands it spawns, a background host with parallel runners, and the hub
that exposes routed events.

> ℹ️ Read `CK.Cris` first, in
> [CK-Cris-Abstractions](https://github.com/signature-opensource/CK-Cris-Abstractions): commands,
> events, parts and the seven handler attributes are declared there. This package executes what that
> one declares.

## Two entry points, and the split is validate-here / execute-there.

[`RawCrisReceiver`](RawCrisReceiver.cs) and [`RawCrisExecutor`](RawCrisExecutor.cs) are both process-wide
singletons with generated implementations, and each owns one half of the pipeline:

```csharp
// At the endpoint that receives the command.
Task<CrisValidationResult> IncomingValidateAsync( IActivityMonitor monitor,
                                                  IServiceProvider services,
                                                  ICrisPoco crisPoco,
                                                  IDisposableGroup? logGroup = null,
                                                  CurrentCultureInfo? currentCulture = null );

// Wherever the command is actually executed.
Task<RawResult> RawExecuteAsync( IServiceProvider services, IAbstractCommand command );
```

The receiver runs the `[IncomingValidator]` methods, then the `[ConfigureAmbientServices]` ones, and
may produce an `AmbientServiceHub` in `CrisValidationResult.AmbientServiceHub`. That hub is the
handover: it is what lets the command be executed in a container other than the one that received it.

"May" is exact. `GetFinalHub` returns the clone only if it `IsDirty` - a configurator that runs and
changes nothing leaves the result hub **null**, and the command executes in the receiving container.
Having a configurator is not the same as needing a handover.

The executor runs the `[CommandHandlingValidator]` methods, the handler and the post handlers -
explicitly *not* the incoming validators. It also has `RestoreAmbientServicesAsync` for the reverse
situation: a command reaching a background context with no hub, whose `[RestoreAmbientServices]`
methods rebuild one.

`IServiceProvider` is a parameter rather than a field on purpose. The class summary says it: *"This
class is agnostic of the context since the IServiceProvider defines the execution context: this is a
true singleton, the same instance can be used to execute any locally handled commands."*

## Almost nothing throws, and the exception is worth knowing.

This is the property to rely on, and it is not uniform - one method breaks the pattern:

| Method | On failure |
|--------|-----------|
| `IncomingValidateAsync` | a `CrisValidationResult` carrying the messages - exceptions are caught, logged and turned into messages |
| `RawExecuteAsync` | an `ICrisResultError` as the `RawResult.Result` |
| `RestoreAmbientServicesAsync` | a non-null error and a null hub |
| `DispatchEventAsync` | **throws** - the exception is set on the returned task |
| `SafeDispatchEventAsync` | logs and returns false |

`DispatchEventAsync` is the exception, and `SafeDispatchEventAsync` exists precisely because of it -
it wraps the call, logs under an error group, and falls back to `ActivityMonitor.StaticLogger` when
the service provider has no monitor.

A command with no handler is not a crash either. The generated code carries a default implementation
that produces an error result with the message code `Cris.MissingCommandHandler`, naming the command.

## Running a command is three resolutions and a call.

```csharp
using( var scope = auto.Services.CreateScope() )
{
    var services = scope.ServiceProvider;
    var executor = services.GetRequiredService<CrisExecutionContext>();
    var command = services.GetRequiredService<PocoDirectory>().Create<IStupidCommand>( c => c.Message = "Run!" );
    var executed = await executor.ExecuteRootCommandAsync( command );
}
```

From [`CrisExecutionContextTests`](../Tests/CK.Cris.Executor.Tests/CrisExecutionContextTests.cs). Two
things in there are easy to miss on a first reading of the API.

A command is **never constructed** - `PocoDirectory.Create<T>( configure )` makes it, because the
concrete class is generated and the interface is all you ever name. And the whole thing happens inside
a **scope**: `CrisExecutionContext` is scoped, so resolving it from the root provider is the mistake
to avoid. Its own constructor comment is blunt about that - *"There is unfortunately no safe way to
ensure that this provider is a scoped one, so be cautious."*

`executed` is an `IExecutedCommand<T>`: `Result`, `Events`, `ValidationMessages`.

## The execution context is what a handler is given, and it recurses.

[`CrisExecutionContext`](CrisExecutionContext.cs) is the scoped `ICrisCommandContext` implementation. A
handler receives it, calls `ExecuteCommandAsync` or `EmitEventAsync`, and the context keeps a stack -
`IsExecutingCommand` is true while one is in flight, and `ExecuteRootCommandAsync` must not be called
then.

Failure has a boundary, and it is drawn between immediate and final events:

- if handling an **immediate** event fails, the command fails;
- if a **final routed** event throws once the command has succeeded, it is logged and does not
  surface - *"the command has been successfully executed, the consequences are not its concern"*.

`RaiseImmediateEventAsync` and `RaiseCallerOnlyImmediateEventAsync` are the two `virtual` hooks for a
transport that has to forward events outside the process. The first has a default implementation that
sends to the event hub and must be called by any override; the second does nothing by default.

## Background execution is a job, a runner and an executor.

[`CrisExecutionHost`](CrisExecutionHost/CrisExecutionHost.cs) runs [`CrisJob`](CrisExecutionHost/CrisJob.cs)
instances on a variable number of parallel runners - `ParallelRunnerCount` defaults to 1, is settable
at any time between 1 and 1000, and raises `ParallelRunnerCountChanged`. It is an
`ISingletonAutoService`, and `ICrisExecutionHost` is a *multiple* interface: one host is enough, but
nothing prevents another.

Tuning it is a property assignment on the resolved singleton, at any point in the application's life:

```csharp
_auto.Services.GetRequiredService<CrisExecutionHost>().ParallelRunnerCount = 1;
```

[`ContainerCommandExecutor`](CrisExecutionHost/ContainerCommandExecutor.cs) is the abstract half a DI
container must provide. `PrepareJobAsync` returns *an error xor a configured scope*, and the two
extension points - `OnImmediateEventAsync`, `SetFinalResultAsync` - are called after every local
impact has already happened, so an override adds outward propagation rather than replacing anything.

One `CrisJob` flag repays reading. `IncomingValidationCheck` re-runs the incoming validation on a
command that already crossed it, and it defaults to
`CoreApplicationIdentity.IsDevelopmentOrUninitialized`. Its comment is explicit about what that buys -
*"This coherency check is done in "#Dev" environment but not in production."* - and what it verifies
is that the `[RestoreAmbientServices]` methods rebuilt a container the command would have accepted.
Mind the direction: the check runs **in development and not in production**, so a faulty
reconstruction fails your dev run and goes through in production. That is the intent - catch it at
your desk - but it also means production never re-checks.

A job may also have no `IExecutingCommand` at all: *"An Executing command is on the caller side, it is
one of the possible interface to the execution, not the execution itself."*

## Events reach observers through the hub, and only immediate ones are live.

[`CrisEventHub`](CrisEventHub/CrisEventHub.cs) is a singleton exposing two `PerfectEvent<IEvent>`:
`Immediate` and `All`. Anyone can subscribe, and it is two lines:

```csharp
var hub = services.GetRequiredService<CrisEventHub>();
hub.Immediate.Sync += ( monitor, e ) => immediateEventCollector.Add( e );
hub.All.Sync += ( monitor, e ) => allEventCollector.Add( e );
```

`Sync` is one of the three `PerfectEvent` subscription forms - the other two take an asynchronous
handler that the sender awaits. Raising is on
[`DarkSideCrisEventHub`](CrisEventHub/DarkSideCrisEventHub.cs), a separate singleton holding the
senders - so the ability to observe and the ability to raise are two different services to inject, and
`ImmediateSender` relays into `AllSender` automatically.

On the caller side, [`IExecutingCommand`](ExecutingCommand/IExecutingCommand.cs) carries the command, a
task completing with the `IExecutedCommand`, and a live `ImmediateEvents` collection updated during
execution with an `Added` event. Only `RoutedImmediateEvent` and `CallerOnlyImmediateEvent` appear
there - a non-immediate event exists only once the command has completed, in
`IExecutedCommand.Events`.

## Requires.

- `CK.Cris`, for every declaration this package executes - and it registers `CrisDirectory` itself
  through `[AlsoRegisterType<CrisDirectory>]`.
- `CK.PerfectEvent`, for the hub's events and `ParallelRunnerCountChanged`.

The generated bodies come from an engine assembly named by string in
`[ContextBoundDelegation( "CK.Setup.Cris.RawCrisExecutorImpl, CK.Cris.Executor.Engine" )]`. There is no
reference to it: without that assembly at setup time these classes stay abstract and nothing resolves.
