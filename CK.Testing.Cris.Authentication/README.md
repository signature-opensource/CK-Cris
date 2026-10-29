# CK.Testing.Cris.Authentication

The authentication fakes of the tests, on one actor database (`FakeActorDatabase`):

- **A fake authentication center**, for the tests that run **below** a real one (`AddFakeAuthCenter()`);
- **the fake login providers of a real authentication center**, for the tests that run it without a database
  (`AddFakeLoginProviders()`).

## The fake authentication center

It serves the tests that run below a real authentication center: the tests of the endpoint side of the
authentication (CK.Cris.Authentication and its scheme packages), of the HTTP front (CK.Cris.AspNet.Authentication)
and of the clients (CK.Cris.HttpSender, the Angular packages). The session command handlers run for real; only the
authentication center, that handles their `[Internal]` operations, is a fake.

A test that can run the real authentication center (a repository downstream of CK-AppIdentity-Transport) uses it
instead: see CK.Testing.AppIdentity.Cris.AuthCenter.

### The engine configuration

`AddFakeAuthCenter()` (an extension of `BinPathConfiguration`, in the `CK.Testing` namespace) adds the
`FakeAuthCenterTypes` to the BinPath: the three fakes below, and the commands and Pocos that they handle. The test adds
what it tests: the endpoint (CK.Cris.Authentication's `AuthenticationSessionEndpointService` and
`AuthenticationSessionHandlers`, the scheme handlers, or the packages that bring them), the HTTP front...

### The fakes

| Type | Role |
|---|---|
| [`FakeActorDatabase`](FakeActorDatabase.cs) | The `IActorInfoProvider`: "System" (1), "Alice" (3711), "Albert" (3712), "Robert" (3713), "Hubert" (3714) and the weak actor "Operator" (42). Alice, Albert and Hubert have the "Basic" scheme (password `"success"`), Hubert also "Google". The "Weak" login is passwordless and accepts any actor (a test convenience). Mutable (`AllActors`, `Disabled`), everything is virtual. |
| [`FakeAuthCenterOperations`](FakeAuthCenterOperations.cs) | The operations: open a session ("Basic", "Weak"), refresh, logout, impersonate and clear it, the login schemes, the remote logins and the passkey ceremonies. Its sessions and tokens live in memory. It records the last operations (`LastOpen`, `LastRefresh`...) for the tests of the protocol. |
| [`FakeTokenReader`](FakeTokenReader.cs) | The `IAuthenticationTokenReader` (scoped): it reads the tokens issued by `FakeAuthCenterOperations`. |

The behavior of `FakeAuthCenterOperations` (its properties adjust it):

- "Basic": a Normal session (`BasicCriticalLifetime` gives a Critical level); the password `"critical-1s"` gives a
  Critical level that ends one second later.
- "Weak": any actor of the database, passwordless (the weak actor "Operator" is at the Unsafe level).
- Passkeys: the challenges are `"login-challenge"` and `"registration-challenge"`; a login response that contains
  `"signed":"login-challenge"` opens a Critical session for `PasskeyActorId` (Albert).
- Remote logins: the provider's URL is `https://idp.example.com/authorize?state=...` (`prompt=login&` for a Critical
  login); the callback is accepted from the browser that started it (its presented correlation) with the code
  `"the-code"`, for `RemoteLoginActorId` (Albert).
- The login schemes: "Basic" (5 minutes of Critical level), "Weak" and "Passkey" (5 minutes), see `LoginSchemes`.
- The sessions last `SessionLifetime` (1 hour), their device is `DeviceId` ("the-device").

The tests get the instance (a singleton) from their services: `services.GetRequiredService<FakeAuthCenterOperations>()`.

## The login providers of a real authentication center

`AddFakeLoginProviders( withPasskeys )` adds the `FakeActorDatabase` and the `ILoginProvider`s of
[FakeLoginProviders.cs](FakeLoginProviders.cs), for a test that runs the real authentication center
(CK.AppIdentity.Cris.AuthCenter.Local) without a database:

| Type | Scheme |
|---|---|
| `FakeBasicLoginProvider` | "Basic": the `IBasicLoginCommand` against the database (records `LastPassword`). |
| `FakeWeakLoginProvider` | "Weak": any actor of the database, passwordless. |
| `FakeOidcLoginProvider` | "Oidc": its payload is a `FakeOidcPayload`; the identities bound to an actor are its `Bindings` ("albert-sub" and "robert-sub" in the "Test" scheme). Records `LastPayload`. |
| `FakePasskeyLoginProvider`, `InMemoryPasskeyStore` | "Passkey" (with `withPasskeys`): the credentials in memory. |
