# Delayed command execution

An in-memory implementation of the delayed command contract: a priority queue, a timer, and a handoff
to the background executor when the date arrives.

> ℹ️ `IDelayedCommand` and `IDelayedCommandExecutedEvent` are declared in `CK.IO.DelayedCommand`, in
> [CK-Cris-Abstractions](https://github.com/signature-opensource/CK-Cris-Abstractions). Read
> [CK.Cris.BackgroundExecutor](../CK.Cris.BackgroundExecutor/README.md) too: the delayed command is
> submitted there when its turn comes.

## In memory, and honest about it.

[`CrisDelayedCommandService`](CrisDelayedCommandService.cs) is *"Simple basic implementation of an
in-memory only service"*. Nothing survives a restart. It is a working default, not a scheduler.

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

`delayed` then goes wherever your commands go - it is an ordinary `ICommand`, and this package's
`[CommandHandler]` is what picks it up. This repository has no test or sample for this package, so
that snippet is written from the declarations rather than lifted from a fixture.

## Three seams, and the useful one runs under a lock.

`OnCommandExecuting` is called when an entry is handed to the executor, and its summary carries a
warning that is easy to miss:

> Caution: This is called from the timer thread while an internal lock is held on the memory store.
> Whatever is implemented here must be fast.

Doing I/O there - writing "executed" to a database, say - stalls every other pending command. The
work belongs on the completion path instead, which runs in the execution context, on the
`CrisExecutionHost` runner's monitor, with the configured scoped services. Getting onto that path is
the awkward part: `OnExecutedCommandAsync` is `protected` but **not** `virtual`, and `OnTimer` passes
the base method group straight to `Submit`, so shadowing it changes nothing. Nor is there a seam to
route around it - `MemoryStore` takes no callback parameter and the background executor service is a
private field with no protected accessor. Getting your own completion callback in means resolving
`CrisBackgroundExecutorService` yourself and submitting directly, which abandons the queue and the
timer this class exists for.

The completion callback receives an `IServiceProvider?`, and the null case is documented precisely: it is
null *"if and only if the scoped services could not be created because ambient services failed to be
restored"*. A `[RestoreAmbientServices]` method that threw is the one situation where the command was
never given a container.

## The validation is where the date rule lives.

```csharp
public ValueTask ValidateCommandAsync( ICrisIncomingValidationContext c, IDelayedCommand command )
```

It checks that `IDelayedCommand.Command` is valid and that `ExecutionDate` is in the future unless
`AllowPastExecutionDate` is set. The parameter type is the reason to point at it: an
`ICrisIncomingValidationContext` rather than a `UserMessageCollector`, because the *inner* command has
to be validated too, and `ValidateAsync( crisPoco )` is what does it. This is the composite-validation
case the interface exists for.

## The completion event is emitted from the callback.

`OnExecutedCommandAsync` dispatches an `IDelayedCommandExecutedEvent` carrying the executed command
and its result. Since that event is `[RoutedEvent]`, it reaches any `[RoutedEventHandler]` in the
process - which is how something learns the delayed command ran without holding a reference to
anything here.

[`DelayedCommandEntry`](DelayedCommandEntry.cs) is the waiting entry, and it is also an
`IDeferredCommandExecutionContext`: it carries the `IssuerToken` that correlates the submission with
the eventual execution logs, a `MemorySequenceId`, and an `ExecutingCommand` task. Its XML says that
task completes when the inner command starts; it is set right after `Submit` returns, so what it
really marks is the command being **queued** on the execution host, before any runner picks it up.
Awaiting it gives you the `IExecutingCommand`, whose own executed-command task is the one that
completes at the end - two stages, because the entry exists long before the execution does.

## Requires.

- `CK.Cris.BackgroundExecutor`, where the command is submitted.
- `CK.Cris`, for `IExecutedCommand`, `ICrisIncomingValidationContext` and the handler attributes.
- `CK.IO.DelayedCommand`, for `IDelayedCommand` and `IDelayedCommandExecutedEvent`.
