using CK.Core;
using CK.Cris;
using CK.IO.Authentication;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Auth;

/// <summary>
/// The session side of the endpoint that received a session command: it executes the internal session operations
/// (<c>[Internal]</c> commands of CK.IO.Authentication) with the data of the <see cref="IAuthenticationSessionEndpoint"/>
/// and applies their results to it. It is also the <see cref="IAuthenticationSessionOpener"/>.
/// <para>
/// Where an operation runs is a deployment detail: it is handled locally by the authentication center, and dispatched
/// to it (<see cref="ICrisRemoteDispatcher"/>) from any other party. The secrets never leave the endpoint: only their
/// hashes travel with the operations.
/// </para>
/// <para>
/// An operation that fails without deciding anything (the authentication center is unreachable for instance) is thrown
/// as an <see cref="MCException"/>: the session command then ends with an error result ("nothing decided"), never with a
/// failed <see cref="ISessionResult"/> (that is a decision of the authentication center).
/// </para>
/// </summary>
public sealed class AuthenticationSessionEndpointService : IAuthenticationSessionOpener
{
    readonly IAuthenticationSessionEndpoint _endpoint;
    readonly AuthenticationInfo _current;
    readonly RawCrisExecutor _executor;
    readonly IServiceProvider _services;
    readonly PocoDirectory _pocos;

    /// <summary>
    /// Initializes a new endpoint service.
    /// </summary>
    /// <param name="endpoint">The endpoint that received the session command.</param>
    /// <param name="current">The current authentication.</param>
    /// <param name="executor">The executor of the operations.</param>
    /// <param name="services">The services of the endpoint (the operations are executed in this scope).</param>
    /// <param name="pocos">The poco directory.</param>
    public AuthenticationSessionEndpointService( IAuthenticationSessionEndpoint endpoint,
                                                 AuthenticationInfo current,
                                                 RawCrisExecutor executor,
                                                 IServiceProvider services,
                                                 PocoDirectory pocos )
    {
        _endpoint = endpoint;
        _current = current;
        _executor = executor;
        _services = services;
        _pocos = pocos;
    }

    /// <summary>
    /// Gets the endpoint that received the session command.
    /// </summary>
    public IAuthenticationSessionEndpoint Endpoint => _endpoint;

    /// <summary>
    /// Gets the current authentication.
    /// </summary>
    public AuthenticationInfo Current => _current;

    /// <summary>
    /// Gets the poco directory.
    /// </summary>
    public PocoDirectory PocoDirectory => _pocos;

    /// <inheritdoc />
    public async ValueTask<ISessionResult> OpenSessionAsync<T>( IActivityMonitor monitor,
                                                                T command,
                                                                string scheme,
                                                                object payload,
                                                                bool rememberMe = false,
                                                                IReadOnlyDictionary<string, string?>? userData = null )
        where T : ICommand<ISessionResult>, ICommandAuthenticationSession
    {
        Throw.CheckNotNullArgument( scheme );
        if( payload is not IAbstractCommand login )
        {
            return Throw.ArgumentException<ISessionResult>( nameof( payload ), "The payload of a login must be the login command." );
        }
        var op = _pocos.Create<IOpenSessionOperationCommand>( c =>
        {
            c.Scheme = scheme;
            c.Payload = login;
            c.RememberMe = rememberMe;
            if( userData != null )
            {
                foreach( var (k, v) in userData ) if( v != null ) c.UserData[k] = v;
            }
        } );
        return await ExecuteSessionOperationAsync( monitor, op ).ConfigureAwait( false )
               ?? Throw.InvalidOperationException<ISessionResult>( "A login operation must have a result." );
    }

    /// <summary>
    /// Refreshes the presented session.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>The session result.</returns>
    public async ValueTask<ISessionResult> RefreshAsync( IActivityMonitor monitor )
    {
        return await ExecuteSessionOperationAsync( monitor, _pocos.Create<IRefreshSessionOperationCommand>() ).ConfigureAwait( false )
               ?? Throw.InvalidOperationException<ISessionResult>( "A refresh operation must have a result." );
    }

    /// <summary>
    /// Revokes the presented session. The endpoint's secrets are cleared whatever happens.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>The awaitable.</returns>
    public async ValueTask LogoutAsync( IActivityMonitor monitor )
    {
        try
        {
            await ExecuteSessionOperationAsync( monitor, _pocos.Create<ILogoutSessionOperationCommand>() ).ConfigureAwait( false );
        }
        finally
        {
            _endpoint.Clear();
        }
    }

    /// <summary>
    /// Impersonates in the presented session.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The impersonate command.</param>
    /// <returns>The session result.</returns>
    public async ValueTask<ISessionResult> ImpersonateAsync( IActivityMonitor monitor, IImpersonateCommand command )
    {
        var op = _pocos.Create<IImpersonateOperationCommand>( c =>
        {
            c.ActorId = _current.UnsafeActor.ActorId;
            c.TargetActorId = command.TargetActorId;
            c.TargetActorName = command.TargetActorName;
        } );
        return await ExecuteSessionOperationAsync( monitor, op ).ConfigureAwait( false )
               ?? Throw.InvalidOperationException<ISessionResult>( "An impersonate operation must have a result." );
    }

    /// <summary>
    /// Clears the impersonation of the presented session.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>The session result.</returns>
    public async ValueTask<ISessionResult> ClearImpersonationAsync( IActivityMonitor monitor )
    {
        return await ExecuteSessionOperationAsync( monitor, _pocos.Create<IClearImpersonationOperationCommand>() ).ConfigureAwait( false )
               ?? Throw.InvalidOperationException<ISessionResult>( "A clear impersonation operation must have a result." );
    }

    /// <summary>
    /// Starts a remote login: the endpoint must be able to receive the remote login callback.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The start login command.</param>
    /// <returns>The URL to which the caller is redirected.</returns>
    public async ValueTask<RedirectedUrl> StartRemoteLoginAsync( IActivityMonitor monitor, IStartLoginCommand command )
    {
        var callbackUrl = _endpoint.RemoteLoginCallbackUrl;
        if( callbackUrl == null )
        {
            throw new MCException( MCString.CreateNonTranslatable( NormalizedCultureInfo.CodeDefault,
                                                                   "This endpoint cannot receive remote logins." ) );
        }
        var op = _pocos.Create<IStartRemoteLoginOperationCommand>( c =>
        {
            c.Login = command;
            c.CallbackUrl = callbackUrl;
            c.CorrelationHash = _endpoint.PrepareRemoteLogin();
            c.WebOrigin = _endpoint.WebOrigin;
        } );
        return await ExecuteOperationAsync( monitor, op ).ConfigureAwait( false );
    }

    /// <summary>
    /// Completes a remote login.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The callback command built by the endpoint.</param>
    /// <returns>The session result.</returns>
    public async ValueTask<ISessionResult> CompleteRemoteLoginAsync( IActivityMonitor monitor, IRemoteLoginCallbackCommand command )
    {
        var op = _pocos.Create<ICompleteRemoteLoginOperationCommand>( c =>
        {
            c.Callback = command;
            c.PresentedCorrelationHash = _endpoint.PresentedRemoteLoginHash;
        } );
        return await ExecuteSessionOperationAsync( monitor, op ).ConfigureAwait( false )
               ?? Throw.InvalidOperationException<ISessionResult>( "A remote login operation must have a result." );
    }

    /// <summary>
    /// Executes an operation that may issue or clear a session: its <see cref="ICommandPresentedSession"/> and
    /// <see cref="ICommandNextSessionSecrets"/> parts are filled from the endpoint, then its result is applied to the
    /// endpoint (the prepared secrets are issued, or the endpoint is cleared).
    /// </summary>
    /// <typeparam name="TOp">The operation type.</typeparam>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The session result (null for an operation without result, the logout).</returns>
    public async Task<ISessionResult?> ExecuteSessionOperationAsync<TOp>( IActivityMonitor monitor, TOp operation )
        where TOp : class, ICommand<ISessionOperationResult>
    {
        Throw.CheckNotNullArgument( operation );
        string? nextRefresh = null;
        string? nextBinding = null;
        if( operation is ICommandNextSessionSecrets next )
        {
            (nextRefresh, nextBinding) = _endpoint.PrepareSecrets();
            next.NextRefreshSecretHash = nextRefresh;
            next.NextBindingSecretHash = nextBinding;
        }
        var r = await ExecuteOperationAsync( monitor, operation ).ConfigureAwait( false );
        if( r.Cleared ) _endpoint.Clear();
        if( r.IssuedSessionKey != null )
        {
            var issued = _endpoint.Issue( r.IssuedSessionKey, r.RememberMe, r.RefreshExpires );
            if( issued.RefreshSecretHash != nextRefresh || issued.BindingSecretHash != nextBinding )
            {
                // The endpoint doesn't honor PrepareSecrets: its secrets are not the ones the authentication center stored.
                _endpoint.Clear();
                Throw.InvalidOperationException( "The endpoint did not issue the prepared secrets." );
            }
        }
        return r.Result;
    }

    /// <summary>
    /// Executes an operation: its <see cref="ICommandPresentedSession"/> part is filled from the endpoint. Executed
    /// locally when it is handled here, dispatched otherwise.
    /// </summary>
    /// <typeparam name="TResult">The operation's result type.</typeparam>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The result.</returns>
    public async Task<TResult> ExecuteOperationAsync<TResult>( IActivityMonitor monitor, ICommand<TResult> operation )
    {
        Throw.CheckNotNullArgument( operation );
        if( operation is ICommandPresentedSession presented && _endpoint.PresentedProof is { } proof )
        {
            presented.PresentedSessionKey = proof.SessionKey;
            presented.PresentedRefreshSecretHash = proof.RefreshSecretHash;
            presented.PresentedBindingSecretHash = proof.BindingSecretHash;
        }
        var raw = await _executor.RawExecuteAsync( _services, operation ).ConfigureAwait( false );
        if( raw.Result is TResult result ) return result;
        if( raw.Result is ICrisResultError error && error.Errors.Count > 0 )
        {
            monitor.Warn( $"Operation '{operation.CrisPocoModel.PocoName}' failed: {error.Errors[0].Text}" );
            throw new MCException( error.Errors[0].Message );
        }
        return Throw.InvalidOperationException<TResult>( $"Operation '{operation.CrisPocoModel.PocoName}' returned an unexpected result." );
    }
}
