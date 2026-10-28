# CK.Cris.Authentication.Basic

The endpoint handler of the "Basic" login: [`BasicLoginHandler`](BasicLoginHandler.cs) handles the
`IBasicLoginCommand` (CK.IO.Authentication.Basic) where it is received and opens the session with the
`IAuthenticationSessionOpener` of [CK.Cris.Authentication](../CK.Cris.Authentication/README.md):

```csharp
[CommandHandler]
public ValueTask<ISessionResult> HandleAsync( IActivityMonitor monitor, IBasicLoginCommand cmd, IAuthenticationSessionOpener opener )
    => opener.OpenSessionAsync( monitor, cmd, "Basic", cmd, cmd.RememberMe );
```

The command is the payload of the "Basic" `ILoginProvider`, which checks the password on the authentication center
(CK.DB.User.UserPassword): this package has no database and can be installed on any party that receives Basic logins.
The password is `[Sensitive]`: it is protected by the dispatch when the login operation crosses a party boundary.
