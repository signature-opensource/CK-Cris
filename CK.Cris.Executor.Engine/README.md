# Execution code generator

Two code generators, one per abstract class of the executor. They turn the model discovered by
[CK.Cris.Engine](../CK.Cris.Engine/README.md) into the bodies of `RawCrisExecutor` and
`RawCrisReceiver`.

> ℹ️ An engine assembly: loaded at setup time, never referenced by an application. It is pulled in by
> `[assembly: CK.Setup.RequiredEngine( "CK.Cris.Executor.Engine" )]` on `CK.Cris.Executor` rather than
> by carrying `IsEngine` itself. Test projects reference it directly.

## Two files, two `CSCodeGeneratorType`, and the same first move.

[`RawCrisExecutorImpl`](RawCrisExecutorImpl.cs) and [`RawCrisReceiverImpl`](RawCrisReceiverImpl.cs)
both open with a type assertion, then ask for the model and step aside if it is not ready yet:

```csharp
var crisEngineService = c.CurrentRun.ServiceContainer.GetService<ICrisDirectoryServiceEngine>();
if( crisEngineService == null ) return CSCodeGenerationResult.Retry;
```

`Retry` rather than failure is the ordering mechanism: `CK.Cris.Engine` publishes
`ICrisDirectoryServiceEngine` only at the very end of its own multi-step run, and these two generators
are re-run afterwards. Neither declares a dependency on that ordering - they just retry.

The assertion is there because a `ContextBoundDelegation` string is not type-checked. The two use
different guards - `Throw.CheckState` in the executor, `Throw.CheckArgument` in the receiver - for the
same check:

```csharp
Throw.CheckState( "Applies only to the RawCrisExecutor class.", classType == typeof( RawCrisExecutor ) );
```

## The generated code lives on the Poco classes, not in a dispatcher.

This is the design decision worth understanding. There is no switch on a command type and no
dictionary lookup at execution time. Instead, each generated Poco implementation class is made to
implement an interface, and the handler call is a method on the command itself:

```csharp
[StObjGen]
interface ICrisExecutorImpl : ICrisPoco
{
    Task<RawCrisExecutor.RawResult> ExecCommandAsync( IServiceProvider s ) { /* default */ }
}
```

`ICrisExecutorImpl` is emitted at namespace scope, so it is internal to the generated assembly.
`RawCrisReceiverImpl` does the same with `IncomingValidateAsync`, but on
`RawCrisReceiver.ICrisReceiverImpl` - a *public* nested interface hand-written in the runtime class
rather than generated. So executing a command is a virtual call on the command object - the dispatch
is the CLR's.

Both interfaces carry a **default implementation** for the empty case, and that is why a missing piece
degrades rather than crashes:

- no command handler - the default `ExecCommandAsync` builds an `ICrisResultError` with the message
  code `Cris.MissingCommandHandler`, naming the command, translated when a `CurrentCultureInfo` is
  resolvable and in the default culture otherwise;
- no incoming validator and no ambient service configurator - the default `IncomingValidateAsync`
  returns `ValueTask.CompletedTask`, and no method is generated for that command at all.

The comment on the first one explains why the interface extends `ICrisPoco` rather than standing
alone: *"We need the CrisPocoModel.PocoName: this is why this specializes ICrisPoco."* The error
message has to name the command, and only the command knows its own name.

## Async is decided per command, not per generator.

`RawCrisReceiverImpl` inspects the model before emitting:

```csharp
bool needAsyncStateMachine = e.IncomingValidators.AsyncHandlerCount > 0 || e.AmbientServicesConfigurators.AsyncHandlerCount > 0;
if( needAsyncStateMachine )
{
    f.Definition.Modifiers |= Modifiers.Async;
}
```

A command whose validators are all synchronous gets a method with no state machine. That is the
recurring justification for generating code here rather than reflecting at runtime: the cost is paid
per *command shape*, at setup, and what ships is what that shape needs.

[`VariableCachedServices`](../CK.Cris.Engine/VariableCachedServices.cs) - from the other engine -
plays the same role for service resolution, hoisting each service out of the provider once per
generated method instead of once per handler call.

## Requires.

- `CK.Cris.Engine`, for `ICrisDirectoryServiceEngine`, `CrisType` and the handler lists that drive the
  emission.
- `CK.Cris.Executor`, for the two abstract classes being implemented and the shapes they declare -
  `RawCrisExecutor.RawResult`, `RawCrisReceiver.ValidationContext` and `ICrisReceiverImpl`.
