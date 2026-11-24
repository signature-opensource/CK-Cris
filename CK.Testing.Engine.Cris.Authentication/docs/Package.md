Test helpers of the authentication: a fake actor database, a fake authentication center for the tests that run below a
real one, and the fake login providers of a real one.

A fake authentication center for the tests of the endpoint side (the session command handlers, the HTTP front, the
clients): `AddFakeAuthCenter()` adds to a BinPath the `FakeActorDatabase` (the actors, "Albert" 3712 with the password
"success"...), the `FakeAuthCenterOperations` (the operations of the session commands) and the `FakeTokenReader`;
`AddFakeLoginProviders()` adds the fake login providers (Basic, Weak, Oidc, Passkey) of a real authentication center
that a test runs without a database.

See the [README](https://github.com/signature-opensource/CK-Cris/blob/stable/CK.Testing.Engine.Cris.Authentication/README.md).
