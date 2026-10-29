# Delayed command execution

An in-memory implementation of the delayed command contract: a priority queue, a timer, and a handoff
to the background executor when the date arrives.

> ℹ️ `IDelayedCommand` and `IDelayedCommandExecutedEvent` are declared in
> [CK.IO.DelayedCommand](https://github.com/signature-opensource/CK-Cris-Abstractions/blob/stable/CK.IO.DelayedCommand/README.md). Read
> [CK.Cris.BackgroundExecutor](../CK.Cris.BackgroundExecutor/README.md) too: the delayed command is
> submitted there when its turn comes.

## In memory, and honest about it.

[`CrisDelayedCommandService`](CrisDelayedCommandService.cs) is *"Simple basic implementation of an
in-memory only service"*. Nothing survives a restart. It is a working default, not a scheduler: a
command whose `KeepOnlyInMemory` is false is stored all the same, with a warning that it cannot be
persisted.

Persistence is an override rather than a rewrite, and the summary names the three seams:
`StoreAsync`, `OnCommandExecuting`, and if needed the `[CommandHandler]` itself,
`HandleCommandAsync`. `MemoryStore` - the priority queue plus timer management - stays available as
`protected`, so a persistent implementation can store durably *and* keep using it for the near-term
entries. What it cannot do is change what happens once the command has run: see below.

The handler is the shortest method in the class and says what the package does:

> Simply calls `StoreAsync`. Commands are always executed from the background context even if their
> `ExecutionDate` is in the past.

So a past date, when allowed, does not shortcut into inline execution - it takes the same path, only
without waiting. One code path, one set of ambient-service semantics.

Delaying a command is therefore building a second command around it and sending that one:

```csharp
var poco = services.GetRequiredService<PocoDirectory>();
var inner = poco.Create<IMyCommand>( c => c.WantedPower = 3712 );
var delayed = poco.Create<IDelayedCommand>( c =>
{
    c.Command = inner;
    c.ExecutionDate = DateTime.UtcNow.AddMinutes( 30 );
} );
```

`delayed` then goes wherever your commands go - it is an ordinary `ICommand`, and the `CrisDelayedCommandService`
has a `[CommandHandler]` that stores it.

## The IDelayedCommand validation.

The `IDelayedCommand` is validated by:
```csharp
[IncomingValidator]
public ValueTask ValidateCommandAsync( ICrisIncomingValidationContext c, IDelayedCommand command )
```

It checks that `IDelayedCommand.Command` is not null, is not an `ICommandEndpointOnly` (such a
command is executed only by its receiving endpoint), is valid, and that `ExecutionDate` is in the
future unless `AllowPastExecutionDate` is set. The parameter type is the reason to point at it: an
`ICrisIncomingValidationContext` rather than a `UserMessageCollector`, because the *inner* command has
to be validated too, and `ValidateAsync( crisPoco )` is what does it. This is the composite-validation
case the interface exists for.

## The completion event is emitted from the callback.

When its date arrives, the inner command is submitted to the `CrisBackgroundExecutorService` with no
ambient service hub (the `[RestoreAmbientServices]` methods rebuild one from the command) and with
`incomingValidationCheck: true`, so the incoming validation runs again in the background container.

`OnExecutedCommandAsync` dispatches an `IDelayedCommandExecutedEvent` initialized from the executed
command (`IPocoCommandExecutedPart.Initialize`: the command, its validation messages, its result and
its events); its `SourceCommand` is the `IDelayedCommand`. The dispatch goes through `SafeDispatchEventAsync`, so a
failing event handler is logged and does not throw. Since that event is `[LocalEvent]`, it reaches any
`[LocalEventHandler]` in the process - which is how something learns the delayed command ran without
holding a reference to anything here.

When the ambient services cannot be restored (a `[RestoreAmbientServices]` method threw, typically
because the actor of the command cannot be found anymore), the command is not executed and there is no
ambient context to raise the event in: no event is raised, an error is logged and the protected virtual
`OnRestoreFailedAsync( monitor, entry, executedCommand )` is called, with the `ICrisResultError` as the
executed command's result. It does nothing by default; a persistent implementation uses it to mark the
stored command as failed.

[`DelayedCommandEntry`](DelayedCommandEntry.cs) is the waiting entry, and it is also an
`IDeferredCommandExecutionContext`: it carries the `IssuerToken` that correlates the submission with
the eventual execution logs, a `MemorySequenceId`, and an `ExecutingCommand` task that
completes right after `Submit` returns: it marks the command being **queued** on the execution host,
before any runner picks it up.
Awaiting it gives you the `IExecutingCommand`, whose own executed-command task is the one that
completes at the end - two stages, because the entry may exist long before the execution does.

## Requires.

- `CK.Cris.BackgroundExecutor`, where the command is submitted.
- `CK.Cris`, for `IExecutedCommand`, `ICrisIncomingValidationContext` and the handler attributes.
- `CK.IO.DelayedCommand`, for `IDelayedCommand` and `IDelayedCommandExecutedEvent`.
