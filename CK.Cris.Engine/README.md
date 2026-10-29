# Cris setup-time engine

The assembly that the Cris attributes name by string. It runs at setup time, discovers every command,
event and handler method, checks that they fit together, and exposes the result to the code
generators.

> ℹ️ This assembly carries `[assembly: CK.Setup.IsEngine()]`. An application never references it - it
> is resolved by name at setup time. A test project may and does: every test project of this
> repository references an engine by `ProjectReference`, which is how they get code generation at all.

## It is reached by name, not by reference.

The seven handler attributes of `CK.Cris`, plus `[AmbientServiceValue]`, are a
`ContextBoundDelegationAttribute` carrying a string:

```csharp
: base( "CK.Setup.Cris.CommandHandlerAttributeImpl, CK.Cris.Engine" )
```

The types in [`AttributeImpl/`](AttributeImpl) are the other end of those eight strings, plus
[`BaseHandlerAttributeImpl`](AttributeImpl/BaseHandlerAttributeImpl.cs), the base of all seven handler
implementations. Two more name-couplings exist beside the attributes:
`[assembly: RequiredEngine( "CK.Cris.Engine" )]` on `CK.Cris`, and the `ContextBoundDelegation` on
`CrisDirectory` itself, which is the entry point the next section describes. None of them is a
reference: the declarations compile against nothing, and this assembly is resolved only when a setup
runs. An application that ships the declarations without the engine gets no
generated code and no handlers, silently.

## `CrisDirectoryImpl` waits for the Poco type system, then registers everything.

[`CrisDirectoryImpl`](CrisDirectoryImpl.cs) is the entry point, and its `Implement` does almost
nothing:

```csharp
// Skips the purely unified BinPath.
if( c.CurrentRun.ConfigurationGroup.IsUnifiedPure ) return CSCodeGenerationResult.Success;
// Waits for the IPocoTypeSystem.
return new CSCodeGenerationResult( nameof( WaitForPocoTypeSystem ) );
```

The deferral is structural: a Cris command *is* a Poco, so nothing can be discovered before the Poco
type system exists. `WaitForPocoTypeSystem` adds the internal `CrisTypeRegistry` and then defers once
more - its own comment: *"One more step to let the attributes register their handlers. Once done, the
public ICrisDirectoryServiceEngine will be published."* The publication is the last statement of
`DoImplement`, after the ambient values have been settled and everything emitted, so a generator that
asks for the service too early gets null and must retry.

It also registers three types on its own behalf, through `IAttributeContextBoundInitializer`:

```csharp
alsoRegister( typeof( ICrisPocoPart ) );
alsoRegister( typeof( CK.Cris.AmbientValues.IAmbientValues ) );
alsoRegister( typeof( ICrisResultError ) );
```

The comment gives the reason - *"so that tests don't have to register them explicitly: registering
`CrisDirectory` is enough"* - and it is a load-bearing convenience: a test that registers only
`CrisDirectory` still gets a working error type and ambient values Poco.

## `CrisType` is the discovered model, and it is stricter than the runtime one.

[`CrisType`](CrisType.cs) is the engine's view of one command or event, and where `ICrisPocoModel`
exposes a flat `Handlers` array at runtime, this separates them by kind:
`CommandHandler` (at most one), `IncomingValidators`, `AmbientServicesConfigurators`,
`AmbientServicesRestorers`, `HandlingValidators`, `PostHandlers`, `EventHandlers`.

## `CloseRegistration` is the gate, and it runs after everything else.

The registration methods accept handlers without filtering much on the kind of type:
`AddMultiTargetHandler` only drops (with a warning) a `[LocalEventHandler]` on a type that is not a
local event.
The rule by kind lives in [`CrisType.CloseRegistration`](CrisType.cs), which runs once per type, from
`CrisDirectoryImpl`, immediately before emission, and **empties handler lists by kind**:

| Handler | Command | Local event | Caller-only event |
|---------|---------|--------------|-------------------|
| `[IncomingValidator]` | kept, **even when the command has no handler** | erased | erased |
| `[ConfigureAmbientServices]` | kept if handled | **kept if the event has handlers** | erased |
| `[RestoreAmbientServices]` | kept if handled | kept if the event has handlers | erased |
| `[CommandHandlingValidator]`, `[CommandPostHandler]` | kept if handled | erased | erased |
| `[LocalEventHandler]` | - | the handlers themselves | warns at registration |

`IncomingValidators`, `HandlingValidators` and `PostHandlers` are therefore empty on every event kind,
while `AmbientServicesConfigurators` and `AmbientServicesRestorers` are kept on a handled local event.

The validators-on-an-unhandled-command case is deliberate and the code says why:

> Whether the command is handled or not is not the problem of the incoming validators: this enables
> `RawCrisReceiver` to be used in a "relay" gateway.

**Three places warn, not one.** Besides the *"will never be called"* of a `[LocalEventHandler]` on a
type that is not a local event, `CloseRegistration` warns for an unhandled command:
*"Command '...' is not handled. Forgetting N validator, N ambient service configurators, N ambient
service restorers and N post handlers."*, and for an unhandled local event: *"Local event '...' is
not handled. Forgetting N ambient service configurators and N ambient service restorers."* The
"Forgetting" messages are the ones to grep for when a handler silently does nothing.

`IsHandled` combines the two shapes: a command needs its handler, an event needs at least one.

A "multi target" handler is one that *may* target several types: a method taking a **part** hits every
command mixing that part in. It may equally take one concrete command or event, and it still lands in
a [`MultiTargetHandlerList`](HandlerMethods/MultiTargetHandlerList.cs) - the list type says how many
types a kind of handler *can* reach, not how many this one does. The command handler is a single
[`HandlerMethod`](HandlerMethods/HandlerMethod.cs) because a command has at most one.

## The ambient value checks.

[`AmbientServiceValueAttributeImpl`](AttributeImpl/AmbientServiceValueAttributeImpl.cs) checks the
property is nullable and registers it with its `IsSafe` flag, then
[`CrisTypeRegistry.SettleAmbientValues`](CrisTypeRegistry.SettleAmbientValues.cs) pairs each one with
its `IAmbientValues` twin. The two failure directions are deliberately unequal:

- a **surplus** of `[AmbientServiceValue]` properties over `IAmbientValues` fields is an **error**,
  and the message writes out the declaration to add: *"Are you missing a `IXXXAmbientValues :
  IAmbientValues` secondary Poco definition with the following properties?"*;
- an `IAmbientValues` field with **no `[AmbientServiceValue]`** is only an **Info** - a value can
  legitimately be collected without any command carrying it;
- a type mismatch between a pair is an error. The comparison is made against the nullable form of the
  `IAmbientValues` field, so the twin may be declared nullable or not.

A value is **safe** as soon as one of its `[AmbientServiceValue]` definitions has `IsSafe` set. For
each handled command or event, `CrisTypeRegistry.CheckSafeAmbientValues` then **warns** when a safe
ambient value is checked by no `[IncomingValidator]` (none of them takes a parameter that exposes the
property): *"Safe ambient value '...' of '...' is checked by no [IncomingValidator]: any caller can state
it."* It is a warning, not an error.

The rest of the contract is not verified: nothing checks that a safe value has no configurator, nor
that an unsafe one has a configurator, nor that a `[CommandPostHandler]` fills the `IAmbientValues`
twin (a missing post handler leaves the collected value at its default).

An `ICommandEndpointOnly` command that is handled cannot have `[ConfigureAmbientServices]` methods:
this is an error.

## What it produces, and what it does not.

This package builds the model and generates `CrisDirectory`. It does **not** generate the execution
code - `RawCrisExecutor` and `RawCrisReceiver` are implemented by a separate engine that consumes
`ICrisDirectoryServiceEngine` from here. The split lets the directory exist in a build that never
executes a command, such as one that only projects the Poco types to TypeScript.

[`VariableCachedServices`](VariableCachedServices.cs) is the shared helper that both generators use to
emit service resolution once per generated method rather than per handler call.

## Requires.

- `CK.Cris`, the declarations it interprets.
- `CK.StObj.Runtime` for `CSCodeGeneratorType`, the engine map and the attribute infrastructure.
- `CK.CodeGen` for the emitted C#.
