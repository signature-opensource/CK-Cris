# CK.Cris.Authentication.Passkey

The endpoint handlers of the passkey (WebAuthn) commands of CK.IO.Authentication.Passkey: [`PasskeyHandlers`](PasskeyHandlers.cs)
handles them where they are received and runs the passkey operations (`[Internal]` commands) with the
`AuthenticationSessionEndpointService` of [CK.Cris.Authentication](../CK.Cris.Authentication/README.md):

| Command | Operation |
|---|---|
| `IStartPasskeyLoginCommand` | `IStartPasskeyLoginOperationCommand` |
| `IPasskeyLoginCommand` | `IFinishPasskeyLoginOperationCommand` (issues the session at the endpoint) |
| `IStartPasskeyRegistrationCommand` | `IStartPasskeyRegistrationOperationCommand` (Critical, the current actor) |
| `IPasskeyRegistrationCommand` | `IFinishPasskeyRegistrationOperationCommand` (Critical, the current actor) |

Only the authentication center runs the WebAuthn protocol (CK.AppIdentity.Cris.AuthCenter.Passkey): this package has no
database and no WebAuthn library. The endpoint must serve web pages: the origin it checked (`WebOrigin`) travels with the
operations, the authentication center checks it against its configuration and against the origin signed by the browser.
