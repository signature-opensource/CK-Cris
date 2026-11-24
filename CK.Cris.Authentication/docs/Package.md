Gives the Cris authentication parts of CK.IO.Auth their meaning with the CK.AuthenticationInfo model, and defines the
contracts of the authentication endpoints and of the authentication center.

`CrisAuthenticationService` (`CK.Auth`) checks the parts when a command enters the System and restores the
authentication from them when it is executed later or elsewhere.
The endpoint and authentication center contracts are `IAuthenticationSessionEndpoint`, `IAuthenticationSessionOpener`,
`IAuthenticationTokenReader`, the endpoint session service (`AuthenticationSessionEndpointService`, the single
`IAuthenticationSessionOpener`) and the handlers of the session commands shared by all the schemes. It also keeps the
caller party of the deferred commands. An application uses it or CK.Cris.Auth, never both.
