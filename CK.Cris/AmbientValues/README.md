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
something "near the root" changes (like authentication - login/logout, "primary" application route, tenant identifier, etc.).

The very first version used a `Dictionary<string,object>` as the result of this command:
ambient values were not modeled, they were just named values that a Front could use to configure command
properties with the same name. This happened to be a serious problem: client code could never rely on the fact
that a property's value was to be initialized by the ambient values... or not. 

Ambient values are now modeled: the [IAmbientValue](IAmbientValue.cs) command result, like any other IPoco is
extensible from anywhere in the code base and gives a shape to these values.

The stupidly simple singleton auto service [AmbientValuesService](AmbientValuesService.cs) handles the `IAmbientValuesCollectCommand`
by creating a new empty `IAmbientValue` result poco.

That's it for the implementation. This command definition, default command handler and command result are small enough to be defined in this
CK.Cris basic package.

Components/libraries that want to use and publish such ambient values just have to:

- Define an extension to the `IAmbientValue` IPoco with the properties they want. Here we decide that the Ambient Values
must expose an array of strings that are the `UserRoles` of the current user[^1]:

```csharp
  public interface ISecurityAmbientValues : IAmbientValues
  {
      string[] UserRoles { get; set; }
  }
```

Now, we just have to implement a **CommandPostHandler** that provides the information. 
Just like a **CommandHandler**, a **CommandPostHandler**:
- Doesn't require a dedicated class. This can be implemented on a existing `IAutoService` or `IRealObject`.
- Supports parameter injection of singleton as well as scoped services.
- Requires a parameter that is the Command it handles.

One thing differs: when the Command has a result (it's a `ICommand<TResult>`), a **CommandPostHandler** can specify a parameter for
the result so that it can control/enrich/alter this result:

```csharp
  [CommandPostHandler]
  public async Task GetAmbientRolesAsync( IAmbientValuesCollectCommand cmd,
                                          ISqlCallContext ctx,
                                          IAuthenticationInfo info,
                                          RoleTable roles,
                                          ISecurityAmbientValues values )
  {
      var r = await roles.ReadRolesAsync( ctx, info.User.UserId );
      ctx.Monitor.Info( $"User {info.User.UserName}: {r.Count} roles have been read from the database." );
      values.Roles = r.ToArray();
  }
```

The parameter command itself is not used here (this command has no information, its sole purpose is to define its result) but
is required since this is used to identify the command handled by this PostCommandHandler.

From now on, any command or command part that has a nullable `UserRoles` property decorated by the `[AmbientValue]` attribute
can automatically be updated by the sender of a command: the configuration of these properties is automatic. Note that

```csharp
public interface INeedUserRolesCommand
{
    [AmbientValue]
    string[] UserRoles { get; set; }
}
```

[^1]: Please don't do this! Unless your application is a ToDo list, the Security By Roles pattern (SBR) is intrinsically limited and
doesn't scale well in terms of functionalities!
