# CK.Cris.Authentication.Weak

The endpoint handler of the "Weak" login: [`WeakLoginHandler`](WeakLoginHandler.cs) handles the `IWeakLoginCommand`
(CK.IO.Authentication.Weak) where it is received and opens the session with the `IAuthenticationSessionOpener` of
[CK.Cris.Authentication](../CK.Cris.Authentication/README.md). The resulting session is at most `Unsafe`.

The handler is declared `[CommandHandler( AllowUnclosedCommand = true )]`: it also handles the command extended by
CK.IO.Authentication.WeakZone (the `ZoneId` of the weak actor). The payload of the provider is the whole command, so
the zone-aware "Weak" `ILoginProvider` (CK.DB.Zone.WeakActor) reads the zone on the authentication center. This package
has no database and can be installed on any party that receives weak logins.
