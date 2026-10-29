A fake authentication center for the tests of the endpoint side of the authentication (the session command handlers,
the HTTP front, the clients): `AddFakeAuthCenter()` adds to a BinPath the `FakeActorDatabase` (the actors, "Albert"
3712 with the password "success"...), the `FakeAuthCenterOperations` (the operations of the session commands) and the
`FakeTokenReader`.

See the [README](https://github.com/signature-opensource/CK-Cris/blob/stable/CK.Testing.Cris.Authentication/README.md).
