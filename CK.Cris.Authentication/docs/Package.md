Gives the Cris authentication parts of CK.IO.Auth their meaning with the CK.AuthenticationInfo `AuthenticationInfo`: `CrisAuthenticationService`
(`CK.Auth`) checks them when a command enters the System and restores the authentication from them when it is executed later or elsewhere.
It also defines the endpoint and authentication center contracts: `IAuthenticationSessionEndpoint`, `IAuthenticationSessionOpener`,
`IAuthenticationTokenReader`, the endpoint session service (`AuthenticationSessionEndpointService`, the single `IAuthenticationSessionOpener`) and the handlers of the session commands shared by all the schemes, and keeps the caller party of the deferred commands. An application uses it or CK.Cris.Auth, never both.
