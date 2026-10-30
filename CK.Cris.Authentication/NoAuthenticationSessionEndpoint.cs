using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace CK.Auth;

/// <summary>
/// Registers in the global container an <see cref="IAuthenticationSessionEndpoint"/> that throws: only an endpoint that
/// receives the session commands has one (the HTTP front registers its own, that replaces this one).
/// <para>
/// Temporary: the engine registers the <see cref="AuthenticationSessionEndpointService"/> in the global container even
/// where no endpoint provides its <see cref="IAuthenticationSessionEndpoint"/> (a host without HTTP front), and the
/// validation of the container (in Development) fails. Without this registration, the missing endpoint is a silent gap;
/// with it, it is an explicit error if something resolves it.
/// </para>
/// </summary>
public sealed class NoAuthenticationSessionEndpoint : IRealObject
{
    void ConfigureServices( StObjContextRoot.ServiceRegister services )
    {
        services.Services.TryAddScoped<IAuthenticationSessionEndpoint>( _ => throw new InvalidOperationException(
            "No session endpoint here: the session commands are received by an endpoint (the HTTP front for instance)." ) );
    }
}
