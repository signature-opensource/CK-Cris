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
| `IAuthUnsafePart` | `ActorId` is the `UnsafeActor.ActorId` |
| `IAuthNormalPart` | ... and the `UnsafeActor.Level` is `Normal` or `Critical` |
| `IAuthCriticalPart` | ... and the `UnsafeActor.Level` is `Critical` |
| `IAuthDeviceIdPart` | `DeviceId` is the `AuthenticationInfo.DeviceId` |
| `IAuthImpersonationPart` | `ActualActorId` is the `UnsafeActualActor.ActorId` |

The level is read on the actor, not on the session: an administrator that impersonates a weak actor sends
`Unsafe` commands only.

The `IAuthAmbientValues` are the `UnsafeActor`, the `UnsafeActualActor` and the `DeviceId`: an `Unsafe` session
(a weak actor, an expired session) receives its real identifiers and can send the `IAuthUnsafePart` commands.
(The legacy service sent the `User.UserId`, the anonymous at `Unsafe` level, that its own validator then refused.)

## When the command is executed later or elsewhere

A command validated when it entered the System carries trustable identifiers. `RestoreAsync` rebuilds the
`AuthenticationInfo` from them:

- the actors are read by the `IActorInfoProvider` (their current state, not the one of the submission);
- an actor that cannot be found refuses the command: it is never executed as the anonymous;
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
it issues. The authentication center stores and signs hashes, never secrets. The endpoint also keeps the **session key**
that the authentication center gives to `Issue` and presents it back: the session commands (refresh, logout,
impersonation) carry nothing, the key is how the authentication center finds "the current session". It is opaque for
the endpoint and is not a secret. This lives here, not in the basic Cris packages: a System without user authentication
never sees it.

The endpoint also supports the **remote logins** (OpenID Connect, OAuth...) when it can: `RemoteLoginCallbackUrl` is its
callback (null when it cannot receive one, like an AppIdentity channel); `PrepareRemoteLogin()` generates the
correlation secret of a starting login and returns its hash (the authentication center keeps it with the pending login);
`PresentedRemoteLoginHash` is the hash presented when the login completes: only the caller that started a login can
complete it.

`WebOrigin` is the origin of the web page that sent the command, as checked by the endpoint (null when the endpoint
serves no web page, like an AppIdentity channel). The browser signs the origin in the passkey (WebAuthn) responses: the
authentication center accepts a passkey ceremony only from an origin it is configured to accept, and checks that the
signed origin is this one. The value only selects: the configuration of the authentication center is the control.

## Reading the tokens

An endpoint that receives authenticated commands (the HTTP front) establishes their `AuthenticationInfo` with
[`IAuthenticationTokenReader`](IAuthenticationTokenReader.cs), implemented by the authentication center package. The
token is verified locally, with the keys of the authentication center. It carries the hash of the binding secret of its
session: the endpoint checks that the caller presents that secret (the binding cookie over HTTP), so that a stolen token
cannot be used elsewhere.

## Relaying to a remote authentication center

When the authentication center of a local party is a remote one, the session commands are relayed to it by
[`IAuthenticationSessionRelay`](IAuthenticationSessionRelay.cs). The endpoints that accept the session commands call it
**before** the handlers: `TryRelay` returns null when the command must be executed here.

The endpoint takes part in the relay: it presents the caller's bearer token (`PresentedToken`, read again by the
remote authentication center) and it generates the next secrets in advance (`PrepareSecrets`): their hashes travel
with the command, the secrets stay in the endpoint, and its `Issue` then uses them.

The commands executed by the authentication center that are not session commands (`ICommandAuthCenter`: the actor's
self-service on its own login methods, the login schemes) are relayed by
[`IAuthCenterCommandRelay`](IAuthCenterCommandRelay.cs), called **after** the endpoint's incoming validation. It carries
the caller's proof (its token and the hash of its binding secret), not only its actor identifier: the remote
authentication center establishes the identity again, so a front can act only for the callers that use it.

## Opening a session

[`IAuthenticationSessionOpener`](IAuthenticationSessionOpener.cs) turns a login command into a session. It is
implemented by the authentication center and called by the login command handlers. The rule is: **a package that
implements an authentication provider handles its login commands**. Its handler only tells which scheme and payload to use:

```csharp
[CommandHandler]
public ValueTask<ISessionResult> HandleAsync( IActivityMonitor monitor, IWeakLoginCommand cmd, IAuthenticationSessionOpener opener )
    => opener.OpenSessionAsync( monitor, cmd, "Weak", cmd );
```

The login's user data (the `LoginContext.UserData` read by the validators and the account creation and binding
services) come from the command's `ICommandLoginUserData` part. The optional `userData` parameter replaces them: a remote
login callback passes the data kept by the authentication center since the start of the login, the client never carries
them back. [`LoginUserDataValidator`](LoginUserDataValidator.cs) limits the part when a command enters the System: at most
16 entries and 4096 characters (keys and values), since they come from anonymous clients and a remote login keeps them
in memory until its callback.

A Cris handler handles the **whole** command. When a package extends a login command (`IWeakZoneLoginCommand` adds the
`ZoneId` to `IWeakLoginCommand`), the handler of the base command is ignored and the extending package must handle
the extended command: the provider extension (the zone aware weak actors) comes with its handler.

Four types take part in a login. Each one is **not** the others:

| Type | Package | Role | Does NOT |
|---|---|---|---|
| `ILoginProvider` | CK.AuthenticationInfo | Checks the credentials of its schemes against the user store and returns an `ActorLoginResult` | create a session, issue a token, call the validators |
| `IAuthenticationSessionOpener` | CK.Cris.Authentication, implemented by the authentication center | Turns a login command into a session: `ILoginProvider`, `ILoginValidator`s, account creation or binding, session store, signed token, endpoint proof, `ISessionResult` | check credentials itself, know any scheme |
| `IAuthenticationSessionEndpoint` | CK.Cris.Authentication, implemented by each endpoint | Carries the session proof (cookies over HTTP, the channel over AppIdentity) | decide anything: it only transports |
| The session store | the authentication center | Persists the sessions | appear in any contract |

## Requires

- CK.AuthenticationInfo, for `AuthenticationInfo`, `ActorInfo` and `IActorInfoProvider` (an implementation must be available).
- CK.IO.Authentication (and CK.IO.Auth), for the parts, `IAuthAmbientValues`, the session commands and `ISessionResult`.
