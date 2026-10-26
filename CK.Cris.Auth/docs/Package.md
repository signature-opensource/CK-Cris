Gives the Cris authentication parts of CK.IO.Auth (`IAuthUnsafePart`, `IAuthNormalPart`, `IAuthCriticalPart`, `IAuthDeviceIdPart`,
`IAuthImpersonationPart`) their meaning with the CK.Auth.Abstractions `IAuthenticationInfo`. `CrisAuthenticationService` (`CK.Auth`)
validates them when a command is received, restores the authentication from them in a background context and fills the `IAuthAmbientValues`;
every method is virtual. CK.Cris.Authentication is its CK.AuthenticationInfo counterpart: an application uses one or the other.
