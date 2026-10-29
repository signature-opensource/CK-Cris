The authentication fakes of the tests. A fake authentication center for the tests of the endpoint side (the session command handlers,
the HTTP front, the clients): `AddFakeAuthCenter()` adds to a BinPath the `FakeActorDatabase` (the actors, "Albert"
3712 with the password "success"...), the `FakeAuthCenterOperations` (the operations of the session commands) and the
`FakeTokenReader`; `AddFakeLoginProviders()` adds the fake login providers (Basic, Weak, Oidc, Passkey) of a real
authentication center that a test runs without a database.

See the [README](https://github.com/signature-opensource/CK-Cris/blob/stable/CK.Testing.Cris.Authentication/README.md).
