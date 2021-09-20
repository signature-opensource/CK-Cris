# Ambient values

"Ambient values" is a simple and basic mechanism to expose any information that are "global"
to any front that may require them. An example of such information would be a `TenantId` that
is derived from the endpoint address (or the url of the request).

This information is "global" but may (and often should) be contextualized to the receiver endpoint.
Another example is the `ActorId` and/or `AuthenticatedActorId` based on the `IAuthenticationInfo` or
any other authentication middleware.

One of the goal of ambient values is to allow a Command to be easily (and safely) fully configured before
sending it to the receiver: a Command must always be **complete** when it flows across boundaries.

Fronts (client applications) can collect ambient values by sending the [IAmbientValuesCollectCommand](IAmbientValuesCollectCommand.cs)
whenever (and as often) as they want. This occurs typically once at the client initialization and whenever
something "near the root" changes (like authentication - login/logout, "primary" application route, etc.).

The very first version used a `Dictionary<string,object>` as the result of this command:
ambient values were not modeled, they were just named values that a Front could use to configure command
properties with the same name. This happened to be a serious problem: client code could never rely on the fact
that a property's value was to be initialized by the ambient values... or not. 

Ambient values are now modeled: the [IAmbientValue](IAmbientValue.cs) command result, like any other IPoco is
extensible from anywhere in the code base and gives a shape to these values.



This command definition, default command handler and command result are small enough to be defined in this
CK.Cris basic package.



