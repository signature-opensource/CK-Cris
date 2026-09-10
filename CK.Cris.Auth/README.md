# Authentication handlers

One service, one file. It is the implementation behind the authentication parts: it validates them
against the current authentication, and rebuilds an authentication from them in a background context.

> ℹ️ The parts themselves - `IAuthUnsafePart`, `IAuthNormalPart`, `IAuthCriticalPart`,
> `IAuthDeviceIdPart`, `IAuthImpersonationPart` - are declared in `CK.IO.Auth`, in
> [CK-Cris-Abstractions](https://github.com/signature-opensource/CK-Cris-Abstractions). This package
> is what makes them mean something.

## Declaring a part does nothing until this service is registered.

[`CrisAuthenticationService`](CrisAuthenticationService.cs) is an `ISingletonAutoService` carrying
`[IncomingValidator]`, `[RestoreAmbientServices]` and `[CommandPostHandler]` methods for the five
parts. Install it and a command declaring `ICommandAuthNormal` is enforced; omit it and the same
command is a command with an unchecked `ActorId` property.

That is the whole point of the split, and it is worth stating plainly: the guarantee is not in the
declaration, it is here.

## The validation is one chain, and it compares against the unsafe user.

```csharp
if( !crisPoco.ActorId.HasValue )
{
    c.Error( $"Invalid property: ActorId cannot be null." );
}
else if( crisPoco.ActorId != info.UnsafeUser.UserId )
{
    c.Error( "Invalid actor identifier: the provided identifier doesn't match the current authentication." );
}
else if( crisPoco is IAuthCriticalPart )
{
    if( info.Level != AuthLevel.Critical ) { /* error */ }
}
else if( crisPoco is IAuthNormalPart )
{
    if( info.Level < AuthLevel.Normal ) { /* error */ }
}
```

Three things follow from that shape.

**It is `UnsafeUser`, not `User`.** The identifier the command carries is compared against
`IAuthenticationInfo.UnsafeUser.UserId` - the last authenticated user regardless of expiry - and the
freshness question is then answered separately by the level test. Comparing against `User` would
conflate the two, and would make an expired command fail with "wrong actor" instead of "wrong level".
(The method's own summary says `User`; the code and the remark below it say `UnsafeUser`. The code is
what runs.)

**The two level tests are not symmetric.** `Critical` requires `Level != AuthLevel.Critical` to fail -
an exact match. `Normal` requires `Level < AuthLevel.Normal` - at least. So a `Critical` authentication
satisfies a command asking for `Normal`, and the reverse is refused, which is the ordering the parts
promise.

**`else if` means one error at a time.** A null `ActorId` short-circuits everything, and a mismatched
identifier is never accompanied by a level complaint. The user gets the first problem, not a list.

`ValidateDevicePart` and `ValidateImpersonationPart` are the same idea for `DeviceId` against
`IAuthenticationInfo.DeviceId` and `ActualActorId` against `ActualUser`.

Every method is `virtual` on a class the summary calls a *"default implementation [that] may be
specialized if needed"* - so an application can loosen or tighten a rule without touching the parts.

## The reverse direction: rebuilding an authentication from the command.

`RestoreAsync` is the `[RestoreAmbientServices]` method, and it does the opposite of validation:
*"Creates a `IAuthenticationInfo` from the different authentication parts that reflects them."* The
command's `ActorId` and `DeviceId` become an authentication in a container that had none.

`ActualActorId` does **not**, and that looks like a slip rather than a design. The impersonation
branch tests `imp.ActualActorId.Value != user.UserId` and then re-fetches
`crisPoco.ActorId.Value` - the same identifier it already resolved - so the actual user it builds is
never the impersonated one. The value is read to take the branch and then dropped.

Read together, the two directions explain why the values are on the command at all. At the endpoint
they are *checked* against a trusted source and therefore cannot be forged; in the background they
*are* the source, because there is no request to read one from. The command is the only thing that
crossed - which is also why the `ActualActorId` slip matters: on that path there is nothing else to
recover it from.

## The ambient values it publishes are marked temporary.

`GetAuthenticationValues` is a `[CommandPostHandler]` on the ambient values collect command, filling
`IAuthAmbientValues` - `ActorId`, `ActualActorId` and `DeviceId` - from the current authentication.
The class remark flags it as provisional: *"this is temporary: IAmbientValues initialization will soon
be handled by Default Implementation Methods."* Treat the mechanism as stable and this particular
method as scaffolding.

## Requires.

- `CK.IO.Auth`, for the five parts and `IAuthAmbientValues`.
- `CK.Auth.Abstractions`, for `IAuthenticationInfo`, `AuthLevel`, `IAuthenticationTypeSystem` and
  `IUserInfoProvider`, the two services the constructor takes.
