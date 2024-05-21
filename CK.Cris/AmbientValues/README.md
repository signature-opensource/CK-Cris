# AmbientService values

"Ambient Service values" is a simple and basic mechanism to manage any information that must
automatically and transparently flow accross commands and events. They are properties of
command or events (usually of basic types like `int` or `string`).

One of the key aspect of Cris (and more generally to any Command pattern) is that the command
properties describes its execution in totality: it must always be **complete** when it flows
across boundaries.

They are automatically (and safely) configured without bothering the developer, they come from Ambient services
and can be Secured or not:
- A Secured value is validated when it enters a System against one (or more) Ambient services.
  The most obvious example is:

```csharp
public interface ICrisAuthenticated : ICrisPart
{
    [AmbientServiceValue]
    int? ActorId { get; set; }
}
```

- A Non Secured one configures the AmbientServices. A good example is:
```csharp
public interface ICrisCultureAware : ICrisPart
{
    [AmbientServiceValue]
    string? CultureName { get; set; }
}
```

Note that secured doesn't mean hidden or crypted (they could be but this is rather useless). Secured values are
exposed, they just cannot be "injected" thanks to their validation.

## Secured AmbientService Values: where do they come from?

There is no unique (nor simple) answer to this question, mainly because we want to maximize our capacity to
develop _ad hoc_ solutions to different contexts. For instance the `ActorId`:
- May be retrieved from an encrypted cookie or from a bearer token in the request by a web endpoint
- it may be bound to a TCP connection when the connection was opened thanks to the use of user certificates.
- it can be a command line parameter in a console application. 
- it can even be the `ICrisAuthenticated.ActorId` of the command being handled!
  Yes, this is possible: the System is an internal part of an application set, an internal worker that handles
  already validated commands.

These examples shows that the binding to the "trusted" `ActorId` cannot be implemented once for all: it depends
on the endpoint that receives the command or the event (and the party that interacts with the endpoint).

To accomodate all these scenario, we introduce an indierection: the Ambient service `IAuthenticationInfo`.
This service is the source of thruth for any authentication related values. Ambient services are ubiquitous
services that MUST be resolved by Endpoint DI container.

A classical .NET Web API can use any middlewares, authentication services that relies on cookies, bearer token
or anything else to be able to resolve an instance of the `IAuthenticationInfo` that captures the logged in user
of a request.

A console application that reads the `ActorId` from command line can use something as simple as this (the
`CmdLineHelper` and the `SimpleAuthInfo` don't exist but are simple to implement):

```csharp
builder.Services.AddScoped<IAuthenticationInfo>( services => new SimpleAuthInfo( CmdLineHelper.Get<int>( "ActorId" ) );
```

The Endpoint being in charge of providing the trustable Ambient service, validation of the Secured AmbientService value
is then done by a `[IncomingValidator]`:
```csharp
[IncomingValidator]
public virtual void ValidateAuthenticatedPart( UserMessageCollector c, ICrisAuthenticated cmd, IAuthenticationInfo info )
{
    if( cmd.ActorId != info.User.UserId )
    {
        c.Error( "Invalid actor identifier: the command provided identifier doesn't match the current authentication." );
    }
}
```

## Non Secured AmbientService values: where do they go?

Just like the authentication the culture used to handle a command:
- Is optional:
  - not every command require authentication (a `LoginCommand` is by design non authenticated),
    or use another identity than our `IAuthenticationInfo`/`ActorId`.
  - not every command require a culture to be handled.
- If the command gives bith to events and/or other commands and these need authentication or culture
  then we must be able to provide the original ones or sensible default values if the original command
  didn't have them.

The `ICrisCultureAware.CultureName` is associated to the `ExtendedCultureInfo` Ambient service. This defaults
the `NormalizedCultureInfo.CodeDefault` (whereas the `IAuthenticationInfo` defaults to the anonymous user `0`).

The culture is not _validated_ against the Ambient service to which it is bound, instead it _configures_ it.

Nothing prevents the `CultureName` to also be validated (to be syntaxically valid for instance) and this is
exactly what does the `CrisCultureService` to warn the caller that its culture is not a known culture (the
default culture will be used).

```csharp
public class CrisCultureService : IAutoService
{
    [IncomingValidator]
    public void CheckCultureName( UserMessageCollector validator, ICommandWithCurrentCulture cmd )
    {
        var n = cmd.CurrentCultureName;
        if( string.IsNullOrEmpty( n ) || ExtendedCultureInfo.FindExtendedCultureInfo( n ) == null )
        {
            validator.Warn( $"Culture name '{n}' is unkown. It will be ignored." );
        }
    }

    [ConfigureAmbientServices]
    public void ConfigureCurrentCulture( ICommandWithCurrentCulture cmd, AmbientServiceHub ambientServices )
    {
        var n = cmd.CurrentCultureName;
        if( !string.IsNullOrWhiteSpace( n ) )
        {
            var c = ExtendedCultureInfo.FindExtendedCultureInfo( n );
            if( c != null )
            {
                ambientServices.Override( c );
            }
        }
    }
}
```

## The IAmbientValues collector

All the ubiquitous values of a System are centralized by the `IAmbientValues` Poco.
This Poco eventually defines all the ubiquitous values of a System with their non nullable types.
The definition is done by Secondary Pocos that extends the `IAmbientValues` and the
`IAmbientValuesCollectorCommand` returns all the ubiquitous values. This command has no properties:
only its return type matters:
```csharp
public interface IAmbientValuesCollectCommand : ICommand<IAmbientValues>
{
}
```

The name of the secondary Poco doesn't reaaly matter: they're just here to extend the base type definition,
they are often defined besides their corresponding part.

For the culture it is simple:
```csharp
public interface ICultureAwareAmbientValues : IAmbientValues
{
    string CultureName { get; set; }
}
```
For the authentication, it is more surprising:

```csharp
public interface IAuthAmbientValues : IAmbientValues
{
    int ActorId { get; set; }
    int ActualActorId { get; set; }
    string DeviceId { get; set; }
}
```
The collected values cover more than the `ActorId`. This also captures the `ActualActorId` (to manage impersonation)
and `DeviceId` (that identifies the "device"). This is because key authentication information is not limited to the
actor identifier.

Even Fronts (client applications) can collect these values by sending the `IAmbientValuesCollectCommand`
whenever (and as often) as they want. This occurs typically once at the client initialization and whenever
something "near the root" changes (like authentication - login/logout, "primary" application route, tenant identifier, etc.).

It is `[CommandPostHandler]` methods that provides the information. 
Just like a `[CommandHandler]`, a **CommandPostHandler**:
- Doesn't require a dedicated class. This can be implemented on a existing `IAutoService` or `IRealObject`.
- Supports parameter injection of singleton as well as scoped services.
- Requires a parameter that is the Command it handles.

One thing differs: when the Command has a result (it is a `ICommand<TResult>`), a **CommandPostHandler** can specify
a parameter for the result so that it can control/enrich/alter this result:

```csharp
[CommandPostHandler]
public virtual void GetAuthenticationValues( IAmbientValuesCollectCommand command, IAuthenticationInfo authInfo, IAuthAmbientValues values )
{
    values.ActorId = authInfo.User.UserId;
    values.ActualActorId = authInfo.ActualUser.UserId;
    values.DeviceId = authInfo.DeviceId;
}
```

The parameter `command` itself is not used here (this command has no information, its sole purpose is to define its result) but
is required since it identifies the command handled by this PostCommandHandler.

And the `CrisCultureService` implements:

```csharp
[CommandPostHandler]
public virtual void GetCultureAmbientValue( IAmbientValuesCollectCommand cmd, ExtendedCultureInfo c, ICultureAmbientValues values )
{
    values.CurrentCultureName = c.Name;
}
```
