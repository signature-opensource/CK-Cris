Authentication of the Cris commands with the CK.AuthenticationInfo model: the `CK.IO.Auth` parts (`ActorId`,
levels, device, impersonation) are checked against the ambient `AuthenticationInfo` when a command enters the
System, and the authentication is restored from them when the command is executed later or elsewhere.

This replaces CK.Cris.Auth (that works with CK.Auth.Abstractions): an application uses one or the other, never both.
