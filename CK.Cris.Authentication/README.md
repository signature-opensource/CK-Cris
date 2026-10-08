# CK.Cris.Authentication

Checks and restores the [CK.IO.Auth](https://github.com/signature-opensource/CK-Cris-Abstractions/tree/stable/CK.IO.Auth)
parts with the `AuthenticationInfo` ambient service of CK.AuthenticationInfo. It replaces
[CK.Cris.Auth](../CK.Cris.Auth/README.md), that does the same with CK.Auth.Abstractions.

## One or the other, never both

The [`CrisAuthenticationService`](CrisAuthenticationService.cs) has on purpose the same full name as the legacy one,
`CK.Auth.CrisAuthenticationService`: an application that references both packages cannot be set up. Two validators for
the same parts would mean two answers to "who is the caller".

## When the command enters the System

| Part | Check |
|------|-------|
| `IAuthUnsafePart` | `ActorId` is the `UnsafeUser.UserId` |
| `IAuthNormalPart` | ... and the `UnsafeUser.Level` is `Normal` or `Critical` |
| `IAuthCriticalPart` | ... and the `UnsafeUser.Level` is `Critical` |
| `IAuthDeviceIdPart` | `DeviceId` is the `AuthenticationInfo.DeviceId` |
| `IAuthImpersonationPart` | `ActualActorId` is the `UnsafeActualUser.UserId` |

The level is read on the user, not on the session: an administrator that impersonates a weak actor sends
`Unsafe` commands only.

The `IAuthAmbientValues` are the `UnsafeUser`, the `UnsafeActualUser` and the `DeviceId`: an `Unsafe` session
(a weak actor, an expired session) receives its real identifiers and can send the `IAuthUnsafePart` commands.
(The legacy service sent the `User.UserId`, the anonymous at `Unsafe` level, that its own validator then refused.)

## When the command is executed later or elsewhere

A command validated when it entered the System carries trustable identifiers. `RestoreAsync` rebuilds the
`AuthenticationInfo` from them:

- the users are read by the `IUserInfoProvider` (their current state, not the one of the submission);
- a user that cannot be found refuses the command: it is never executed as the anonymous;
- the level is the one required by the parts, and the dates cover the execution
  (`AuthenticationInfo.CreateRestored` and `RestoredValidity`);
- impersonation (`ActualActorId`) and device identifier are restored.

## The session side of the endpoints

[`IAuthenticationSessionEndpoint`](IAuthenticationSessionEndpoint.cs) is what the authentication center needs from the
endpoint that received a session command (login, refresh, logout...). It is a `[ScopedContainerConfiguredService]`:
each endpoint container implements it - the HTTP endpoint with its cookies, an AppIdentity endpoint with a session bound
to the remote party - and the background container doesn't, since a session command is executed by its receiving
endpoint only.

The endpoint generates the session secrets (the refresh secret and the binding secret) and keeps them: it exposes only
their hashes ([`AuthenticationSessionProof`](AuthenticationSessionProof.cs)), the ones the caller presents and the ones
it issues. The authentication center stores and signs hashes, never secrets. This lives here, not in the basic Cris
packages: a System without user authentication never sees it.

## Requires

- CK.AuthenticationInfo, for `AuthenticationInfo`, `UserInfo` and `IUserInfoProvider` (an implementation must be available).
- CK.IO.Auth, for the parts and `IAuthAmbientValues`.
