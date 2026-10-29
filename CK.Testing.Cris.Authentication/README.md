# CK.Testing.Cris.Authentication

A fake authentication center for the tests that run **below** a real one: the tests of the endpoint side of the
authentication (CK.Cris.Authentication and its scheme packages), of the HTTP front (CK.Cris.AspNet.Authentication)
and of the clients (CK.Cris.HttpSender, the Angular packages). The session command handlers run for real; only the
authentication center, that handles their `[Internal]` operations, is a fake.

A test that can run the real authentication center (a repository downstream of CK-AppIdentity-Transport) uses it
instead: see CK.Testing.AppIdentity.Cris.AuthCenter.

## The engine configuration

`AddFakeAuthCenter()` (an extension of `BinPathConfiguration`, in the `CK.Testing` namespace) adds the
`FakeAuthCenterTypes` to the BinPath: the three fakes below, and the commands and Pocos that they handle. The test adds
what it tests: the endpoint (CK.Cris.Authentication's `AuthenticationSessionEndpointService` and
`AuthenticationSessionHandlers`, the scheme handlers, or the packages that bring them), the HTTP front...

## The fakes

| Type | Role |
|---|---|
| [`FakeActorDatabase`](FakeActorDatabase.cs) | The `IActorInfoProvider`: "System" (1), "Alice" (3711), "Albert" (3712), "Robert" (3713), "Hubert" (3714) and the weak actor "Operator" (42). Alice, Albert and Hubert have the "Basic" scheme (password `"success"`), Hubert also "Google". Mutable (`AllActors`, `Disabled`), everything is virtual. |
| [`FakeAuthCenterOperations`](FakeAuthCenterOperations.cs) | The operations: open a session ("Basic", "Weak"), refresh, logout, impersonate and clear it, the login schemes, the remote logins and the passkey ceremonies. Its sessions and tokens live in memory. It records the last operations (`LastOpen`, `LastRefresh`...) for the tests of the protocol. |
| [`FakeTokenReader`](FakeTokenReader.cs) | The `IAuthenticationTokenReader` (scoped): it reads the tokens issued by `FakeAuthCenterOperations`. |

The behavior of `FakeAuthCenterOperations` (its properties adjust it):

- "Basic": a Normal session (`BasicCriticalLifetime` gives a Critical level); the password `"critical-1s"` gives a
  Critical level that ends one second later.
- "Weak": a weak actor of the database (the Unsafe level).
- Passkeys: the challenges are `"login-challenge"` and `"registration-challenge"`; a login response that contains
  `"signed":"login-challenge"` opens a Critical session for `PasskeyActorId` (Albert).
- Remote logins: the provider's URL is `https://idp.example.com/authorize?state=...` (`prompt=login&` for a Critical
  login); the callback is accepted from the browser that started it (its presented correlation) with the code
  `"the-code"`, for `RemoteLoginActorId` (Albert).
- The login schemes: "Basic" (5 minutes of Critical level), "Weak" and "Passkey" (5 minutes), see `LoginSchemes`.
- The sessions last `SessionLifetime` (1 hour), their device is `DeviceId` ("the-device").

The tests get the instance (a singleton) from their services: `services.GetRequiredService<FakeAuthCenterOperations>()`.
