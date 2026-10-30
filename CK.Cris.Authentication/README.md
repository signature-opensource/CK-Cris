# CK.Cris.Authentication

Checks and restores the [CK.IO.Auth](https://github.com/signature-opensource/CK-Cris-Abstractions/tree/stable/CK.IO.Auth)
parts with the `AuthenticationInfo` ambient service of CK.AuthenticationInfo.

## A single validator of the parts

The [`CrisAuthenticationService`](CrisAuthenticationService.cs) has on purpose the same full name as the one of the
deprecated CK.Cris.Auth, `CK.Auth.CrisAuthenticationService`: an application that still references a former version of
CK.Cris.Auth cannot be set up. Two validators for the same parts would mean two answers to "who is the caller".

## When the command enters the System

| Part | Check | Error's resource name |
|------|-------|-----------------------|
| `IAuthUnsafePart` | `ActorId` is the `UnsafeActor.ActorId` | `Authentication.ActorMismatch` |
| `IAuthNormalPart` | ... and the `UnsafeActor.Level` is `Normal` or `Critical` | `Authentication.NormalLevelRequired` |
| `IAuthCriticalPart` | ... and the `UnsafeActor.Level` is `Critical` | `Authentication.CriticalLevelRequired` |
| `IAuthDeviceIdPart` | `DeviceId` is the `AuthenticationInfo.DeviceId` | |
| `IAuthImpersonationPart` | `ActualActorId` is the `UnsafeActualActor.ActorId` | |

A user interface recognizes these resource names: `Authentication.CriticalLevelRequired` means that a new login is
required.

The level is read on the actor, not on the session: an administrator that impersonates a weak actor sends
`Unsafe` commands only.

The `IAuthAmbientValues` are the `UnsafeActor`, the `UnsafeActualActor` and the `DeviceId`: an `Unsafe` session
(a weak actor, an expired session) receives its real identifiers and can send the `IAuthUnsafePart` commands.

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

An endpoint that receives authenticated commands (the HTTP endpoint of front applications) establishes their `AuthenticationInfo` with
[`IAuthenticationTokenReader`](IAuthenticationTokenReader.cs), implemented by the authentication center package. The
token is verified locally, with the keys of the authentication center. It carries the hash of the binding secret of its
session: the endpoint checks that the caller presents that secret (the binding cookie over HTTP), so that a stolen token
cannot be used elsewhere.

## The session commands are executed by their endpoint

Nothing is relayed: a session command (`ICommandEndpointOnly`) is always executed by the endpoint that received it, on
the authentication center as on any other party. Its handler calls the
[`AuthenticationSessionEndpointService`](AuthenticationSessionEndpointService.cs) (scoped), which executes an **internal
operation** (an `[Internal]` command of CK.IO.Authentication) and applies its result to the endpoint:

1. it reads the endpoint ([`IAuthenticationSessionEndpoint`](IAuthenticationSessionEndpoint.cs)): the presented proof,
   the secrets prepared in advance (`PrepareSecrets`: only their hashes travel), the remote login callback and
   correlation, the web origin;
2. it executes the operation: **handled locally by the authentication center, dispatched to it from any other party**
   (`ICrisRemoteDispatcher`): where it runs is a deployment detail, there is no "local" or "remote" implementation;
3. it applies the `ISessionOperationResult`: the endpoint issues the prepared secrets (checked against the hashes the
   authentication center stored) or clears its secrets.

[`AuthenticationSessionHandlers`](AuthenticationSessionHandlers.cs) handles the session commands shared by all the
schemes (refresh, logout, impersonation, the remote login start and callback); the login commands of the schemes are
handled by CK.Cris.Authentication.Basic, .Weak and .Passkey. None of them needs a database: they can be installed on any
party. An operation that fails without deciding anything (the authentication center is unreachable) ends the session
command with an error ("nothing decided"), never with a failed `ISessionResult` (a decision of the authentication
center). The logout clears the endpoint's secrets whatever happens.

The identity of a command that comes through another party carries that party
(`AuthenticationInfo.CallerPartyId`, set at reception, its levels capped by the `ICallerPartyService`).
[`CallerPartyAmbientValuesCapture`](CallerPartyAmbientValuesCapture.cs) keeps it with the deferred commands (the
delayed commands): their restored authentication is completed with it.

## Opening a session

[`IAuthenticationSessionOpener`](IAuthenticationSessionOpener.cs) turns a login command into a session. Its single
implementation is the `AuthenticationSessionEndpointService`: it executes the `IOpenSessionOperationCommand` (the login
command is its payload, the provider's payload). The login handlers live in the per-scheme endpoint packages, never in
the database packages (a party that receives logins may have no database); a handler only tells which scheme to use:

```csharp
[CommandHandler( AllowUnclosedCommand = true )]
public ValueTask<ISessionResult> HandleAsync( IActivityMonitor monitor, IWeakLoginCommand cmd, IAuthenticationSessionOpener opener )
    => opener.OpenSessionAsync( monitor, cmd, ActorInfo.WeakScheme, cmd );
```

The login's user data (the `LoginContext.UserData` read by the validators and the account creation and binding
services) come from the command's `ICommandLoginUserData` part. The optional `userData` parameter replaces them: a remote
login callback passes the data kept by the authentication center since the start of the login, the client never carries
them back. [`LoginUserDataValidator`](LoginUserDataValidator.cs) limits the part when a command enters the System: at most
16 entries and 4096 characters (keys and values), since they come from anonymous clients and a remote login keeps them
in memory until its callback.

A Cris handler handles the **whole** command. When a package extends a login command (`IWeakZoneLoginCommand` adds the
`ZoneId` to `IWeakLoginCommand`), the handler of the base command handles it only if it says so
(`AllowUnclosedCommand = true`): the Weak handler does, the payload carries the zone to the zone aware provider.

Four types take part in a login. Each one is **not** the others:

| Type | Package | Role | Does NOT |
|---|---|---|---|
| `ILoginProvider` | CK.AuthenticationInfo | Checks the credentials of its schemes against the user store and returns an `ActorLoginResult` | create a session, issue a token, call the validators |
| `IAuthenticationSessionOpener` | CK.Cris.Authentication (the endpoint service) | Executes the open session operation; the authentication center's handler of the operation runs `ILoginProvider`, `ILoginValidator`s, account creation or binding, session store, signed token | check credentials itself, know any scheme |
| `IAuthenticationSessionEndpoint` | CK.Cris.Authentication, implemented by each endpoint | Carries the session proof (cookies over HTTP, the channel over AppIdentity) | decide anything: it only transports |
| The session store | the authentication center | Persists the sessions | appear in any contract |

## Requires

- CK.AuthenticationInfo, for `AuthenticationInfo`, `ActorInfo` and `IActorInfoProvider` (an implementation must be available).
- CK.IO.Authentication (and CK.IO.Auth), for the parts, `IAuthAmbientValues`, the session commands and `ISessionResult`.
