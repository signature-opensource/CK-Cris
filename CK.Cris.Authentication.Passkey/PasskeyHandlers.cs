using CK.Core;
using CK.Cris;
using CK.IO.Authentication;
using CK.IO.Authentication.Passkey;
using System.Threading.Tasks;

namespace CK.Auth;

/// <summary>
/// Handles the passkey commands at the endpoint that receives them: each one runs its passkey operation on the
/// authentication center (locally or dispatched), which alone runs the WebAuthn protocol. The endpoint must serve web
/// pages: the origin it checked travels with the operations.
/// </summary>
public sealed class PasskeyHandlers : ISingletonAutoService
{
    static string GetWebOrigin( AuthenticationSessionEndpointService service )
    {
        return service.Endpoint.WebOrigin
               ?? throw new MCException( MCString.CreateNonTranslatable( NormalizedCultureInfo.CodeDefault,
                                                                         "Passkeys require an endpoint that serves web pages." ) );
    }

    /// <summary>
    /// Starts a passkey login.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command.</param>
    /// <param name="service">The endpoint service.</param>
    /// <returns>The request options (JSON).</returns>
    [CommandHandler]
    public Task<string> HandleAsync( IActivityMonitor monitor, IStartPasskeyLoginCommand command, AuthenticationSessionEndpointService service )
    {
        var origin = GetWebOrigin( service );
        return service.ExecuteOperationAsync( monitor, service.PocoDirectory.Create<IStartPasskeyLoginOperationCommand>( c => c.WebOrigin = origin ) );
    }

    /// <summary>
    /// Completes a passkey login.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command.</param>
    /// <param name="service">The endpoint service.</param>
    /// <returns>The session result.</returns>
    [CommandHandler]
    public async Task<ISessionResult> HandleAsync( IActivityMonitor monitor, IPasskeyLoginCommand command, AuthenticationSessionEndpointService service )
    {
        var origin = GetWebOrigin( service );
        var op = service.PocoDirectory.Create<IFinishPasskeyLoginOperationCommand>( c =>
        {
            c.Login = command;
            c.WebOrigin = origin;
        } );
        return await service.ExecuteSessionOperationAsync( monitor, op ).ConfigureAwait( false )
               ?? Throw.InvalidOperationException<ISessionResult>( "A passkey login operation must have a result." );
    }

    /// <summary>
    /// Starts a passkey registration for the current actor.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command.</param>
    /// <param name="service">The endpoint service.</param>
    /// <returns>The creation options (JSON).</returns>
    [CommandHandler]
    public Task<string> HandleAsync( IActivityMonitor monitor, IStartPasskeyRegistrationCommand command, AuthenticationSessionEndpointService service )
    {
        var origin = GetWebOrigin( service );
        var op = service.PocoDirectory.Create<IStartPasskeyRegistrationOperationCommand>( c =>
        {
            c.ActorId = service.Current.UnsafeActor.ActorId;
            c.Start = command;
            c.WebOrigin = origin;
        } );
        return service.ExecuteOperationAsync( monitor, op );
    }

    /// <summary>
    /// Completes a passkey registration.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command.</param>
    /// <param name="service">The endpoint service.</param>
    /// <returns>The registration result.</returns>
    [CommandHandler]
    public Task<IPasskeyRegistrationResult> HandleAsync( IActivityMonitor monitor, IPasskeyRegistrationCommand command, AuthenticationSessionEndpointService service )
    {
        var origin = GetWebOrigin( service );
        var op = service.PocoDirectory.Create<IFinishPasskeyRegistrationOperationCommand>( c =>
        {
            c.ActorId = service.Current.UnsafeActor.ActorId;
            c.Registration = command;
            c.WebOrigin = origin;
        } );
        return service.ExecuteOperationAsync( monitor, op );
    }
}
