# Ambient values

"Ambient values" is a simple and basic mechanism to expose any information that are "global"
to any front that may require them. An example of such information would be a `TenantId` that
is derived from the endpoint address or the url of the request.

This information is "global" but may (and often should) be contextualized to the receiver endpoint.
Another example is the `ActorId` and/or `AuthenticatedActorId` based on the `IAuthenticationInfo` or
any other authentication middleware.

This command definition and default command handler are small enough to be defined in this
CK.Cris basic package.


