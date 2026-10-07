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

## Requires

- CK.AuthenticationInfo, for `AuthenticationInfo`, `UserInfo` and `IUserInfoProvider` (an implementation must be available).
- CK.IO.Auth, for the parts and `IAuthAmbientValues`.
