using CK.Auth;
using CK.Core;
using CK.Cris;
using CK.IO.Authentication;
using CK.IO.Authentication.Basic;
using CK.IO.Authentication.Passkey;
using CK.IO.Authentication.Weak;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CK.Testing;

/// <summary>
/// The fake authentication center: it handles the operations that the session command handlers of an endpoint
/// (CK.Cris.Authentication and its scheme packages) execute, with the actors of the <see cref="FakeActorDatabase"/>.
/// The tokens it issues are read by the <see cref="FakeTokenReader"/>. Its sessions and tokens live in memory (a refresh, an impersonation or a
/// logout acts on the presented session).
/// <list type="bullet">
///     <item>"Basic": <see cref="FakeActorDatabase.BasicLogin(string, string)"/>, Normal level (see <see cref="BasicCriticalLifetime"/>).
///     The <see cref="CriticalOneSecondPassword"/> gives a Critical level that ends one second later.</item>
///     <item>"Weak": <see cref="FakeActorDatabase.WeakLogin(string)"/> (any actor, passwordless).</item>
///     <item>"Passkey": the ceremonies use the challenges <c>"login-challenge"</c> and <c>"registration-challenge"</c>; a
///     login response that contains <c>"signed":"login-challenge"</c> opens a Critical session for the <see cref="PasskeyActorId"/>.</item>
///     <item>Remote logins: the provider's URL is <c>https://idp.example.com/authorize?state=...</c> (with <c>prompt=login&amp;</c>
///     for a Critical login); the callback is accepted with the code <see cref="RemoteLoginCode"/> from the browser that
///     started it, for the <see cref="RemoteLoginActorId"/>. A binding (<see cref="IStartLoginCommand.BindToCurrentActor"/>)
///     starts from a Critical session that is not impersonated, completes while this session is alive and issues no
///     session: the bound identities are recorded (<see cref="Bindings"/>).</item>
/// </list>
/// The last operations are recorded (<see cref="LastOpen"/>...) for the tests of the protocol.
/// </summary>
public class FakeAuthCenterOperations : ISingletonAutoService
{
    /// <summary>
    /// A "Basic" password that opens a session whose Critical level ends one second later.
    /// </summary>
    public const string CriticalOneSecondPassword = "critical-1s";

    /// <summary>
    /// The code that the remote login callback must carry.
    /// </summary>
    public const string RemoteLoginCode = "the-code";

    readonly FakeActorDatabase _actors;
    readonly PocoDirectory _pocos;
    readonly ConcurrentDictionary<string, AuthenticationTokenData> _tokens = new();
    readonly ConcurrentDictionary<string, Session> _sessions = new();
    readonly ConcurrentDictionary<string, RemoteLogin> _remoteLogins = new();

    sealed record Session( int ActualActorId, int ActorId, bool RememberMe, DateTime CriticalExpires );

    // A pending remote login: the correlation of the browser that started it and, for a binding, the session of the actor.
    sealed record RemoteLogin( string CorrelationHash, string? BindingSessionKey );

    /// <summary>
    /// Initializes a new fake authentication center.
    /// </summary>
    /// <param name="actors">The actors.</param>
    /// <param name="pocos">The poco directory.</param>
    public FakeAuthCenterOperations( FakeActorDatabase actors, PocoDirectory pocos )
    {
        _actors = actors;
        _pocos = pocos;
        LoginSchemes = [("Basic", TimeSpan.FromMinutes( 5 )), (ActorInfo.WeakScheme, TimeSpan.Zero), ("Passkey", TimeSpan.FromMinutes( 5 ))];
    }

    /// <summary>
    /// Gets the login schemes returned by the <see cref="IGetLoginSchemesQCommand"/>, with their Critical lifetime.
    /// Defaults to "Basic" (5 minutes), "Weak" and "Passkey" (5 minutes).
    /// </summary>
    public List<(string Name, TimeSpan CriticalLifetime)> LoginSchemes { get; }

    /// <summary>
    /// Gets or sets the Critical lifetime of a "Basic" login. Defaults to <see cref="TimeSpan.Zero"/>: a "Basic" login is
    /// at the Normal level.
    /// </summary>
    public TimeSpan BasicCriticalLifetime { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of the sessions. Defaults to 1 hour.
    /// </summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours( 1 );

    /// <summary>
    /// Gets or sets the device identifier of the issued sessions. Defaults to "the-device".
    /// </summary>
    public string DeviceId { get; set; } = "the-device";

    /// <summary>
    /// Gets or sets the actor of a passkey login. Defaults to Albert (3712).
    /// </summary>
    public int PasskeyActorId { get; set; } = 3712;

    /// <summary>
    /// Gets or sets the actor of a remote login. Defaults to Albert (3712).
    /// </summary>
    public int RemoteLoginActorId { get; set; } = 3712;

    /// <summary>
    /// Gets the identities bound by a remote login (<see cref="IStartLoginCommand.BindToCurrentActor"/>): the scheme
    /// and the actor.
    /// </summary>
    public ConcurrentQueue<(string Scheme, int ActorId)> Bindings { get; } = new();

    /// <summary>
    /// Gets the last <see cref="IOpenSessionOperationCommand"/>.
    /// </summary>
    public IOpenSessionOperationCommand? LastOpen { get; private set; }

    /// <summary>
    /// Gets the last <see cref="IRefreshSessionOperationCommand"/>.
    /// </summary>
    public IRefreshSessionOperationCommand? LastRefresh { get; private set; }

    /// <summary>
    /// Gets the last <see cref="IImpersonateOperationCommand"/>.
    /// </summary>
    public IImpersonateOperationCommand? LastImpersonate { get; private set; }

    /// <summary>
    /// Gets the last <see cref="IStartRemoteLoginOperationCommand"/>.
    /// </summary>
    public IStartRemoteLoginOperationCommand? LastStartRemote { get; private set; }

    /// <summary>
    /// Gets the last <see cref="ICompleteRemoteLoginOperationCommand"/>.
    /// </summary>
    public ICompleteRemoteLoginOperationCommand? LastCompleteRemote { get; private set; }

    /// <summary>
    /// Gets the last <see cref="IStartPasskeyRegistrationOperationCommand"/>.
    /// </summary>
    public IStartPasskeyRegistrationOperationCommand? LastStartRegistration { get; private set; }

    /// <summary>
    /// Gets the last <see cref="IFinishPasskeyRegistrationOperationCommand"/>.
    /// </summary>
    public IFinishPasskeyRegistrationOperationCommand? LastFinishRegistration { get; private set; }

    /// <summary>
    /// Finds a token issued by this authentication center.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The token data or null.</returns>
    public AuthenticationTokenData? FindToken( string token ) => _tokens.TryGetValue( token, out var d ) ? d : null;

    /// <summary>
    /// Opens a session: a new session key, a new token bound to the next binding secret.
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <param name="actualActor">The actual actor.</param>
    /// <param name="actor">The actor (the actual actor when not impersonated).</param>
    /// <param name="rememberMe">Whether the session is remembered.</param>
    /// <param name="criticalLifetime">The Critical lifetime (<see cref="TimeSpan.Zero"/> for the Normal level).</param>
    /// <returns>The operation result.</returns>
    protected virtual ISessionOperationResult Issue( ICommandNextSessionSecrets op, ActorInfo actualActor, ActorInfo actor, bool rememberMe, TimeSpan criticalLifetime )
    {
        var utcNow = DateTime.UtcNow;
        var info = AuthenticationInfo.Create( actualActor,
                                              actor,
                                              utcNow + SessionLifetime,
                                              criticalLifetime > TimeSpan.Zero ? utcNow + criticalLifetime : null,
                                              DeviceId,
                                              utcNow );
        var token = "token-" + Guid.NewGuid().ToString( "N" );
        _tokens[token] = new AuthenticationTokenData( info, op.NextBindingSecretHash );
        var key = $"{actualActor.ActorId}.{actor.ActorId}.{Guid.NewGuid():N}";
        _sessions[key] = new Session( actualActor.ActorId, actor.ActorId, rememberMe, info.CriticalExpires ?? DateTime.MinValue );
        return _pocos.Create<ISessionOperationResult>( r =>
        {
            r.IssuedSessionKey = key;
            r.RememberMe = rememberMe;
            r.RefreshExpires = utcNow + SessionLifetime;
            r.Result = _pocos.Create<ISessionResult>( s =>
            {
                s.Info.InitializeFrom( info );
                s.Token = token;
                s.RememberMe = rememberMe;
            } );
        } );
    }

    /// <summary>
    /// Refuses a login: no session, a failed result with an error message.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <param name="resName">The resource name of the message.</param>
    /// <returns>The operation result.</returns>
    protected virtual ISessionOperationResult Refuse( string text, string resName )
        => _pocos.Create<ISessionOperationResult>( r => r.Result = _pocos.Create<ISessionResult>( s =>
        {
            s.Success = false;
            s.UserMessages.Add( UserMessage.Error( NormalizedCultureInfo.CodeDefault, text, resName ) );
        } ) );

    // The presented session, cleared when it is unknown or one of its actors is not found.
    ISessionOperationResult? Cleared( ICommandPresentedSession op, out Session? session, out ActorInfo? actualActor, out ActorInfo? actor )
    {
        actualActor = actor = null;
        if( op.PresentedSessionKey == null
            || !_sessions.TryGetValue( op.PresentedSessionKey, out session )
            || (actualActor = _actors.Find( session.ActualActorId )) == null
            || (actor = _actors.Find( session.ActorId )) == null )
        {
            session = null;
            return _pocos.Create<ISessionOperationResult>( r =>
            {
                r.Cleared = true;
                r.Result = _pocos.Create<ISessionResult>( s => s.Success = false );
            } );
        }
        return null;
    }

    [CommandHandler]
    public virtual ISessionOperationResult Handle( IOpenSessionOperationCommand op )
    {
        LastOpen = op;
        switch( op.Payload )
        {
            case IBasicLoginCommand basic:
                if( basic.Password == CriticalOneSecondPassword )
                {
                    var a = _actors.Find( basic.UserName );
                    if( a != null && a.HasScheme( "Basic" ) ) return Issue( op, a, a, op.RememberMe, TimeSpan.FromSeconds( 1 ) );
                }
                var basicActor = _actors.BasicLogin( basic.UserName, basic.Password );
                return basicActor != null
                        ? Issue( op, basicActor, basicActor, op.RememberMe, BasicCriticalLifetime )
                        : Refuse( "Invalid credentials.", "Test.InvalidCredentials" );
            case IWeakLoginCommand weak:
                var weakActor = _actors.WeakLogin( weak.WeakActorName );
                return weakActor != null
                        ? Issue( op, weakActor, weakActor, op.RememberMe, TimeSpan.Zero )
                        : Refuse( "Unknown weak actor.", "Test.UnknownWeakActor" );
            default:
                return Refuse( $"Unknown scheme '{op.Scheme}'.", "Test.UnknownScheme" );
        }
    }

    [CommandHandler]
    public virtual ISessionOperationResult Handle( IRefreshSessionOperationCommand op )
    {
        LastRefresh = op;
        var cleared = Cleared( op, out var session, out var actualActor, out var actor );
        if( cleared != null ) return cleared;
        _sessions.TryRemove( op.PresentedSessionKey!, out _ );
        return Issue( op, actualActor!, actor!, session!.RememberMe, TimeSpan.Zero );
    }

    [CommandHandler]
    public virtual ISessionOperationResult Handle( ILogoutSessionOperationCommand op )
    {
        if( op.PresentedSessionKey != null ) _sessions.TryRemove( op.PresentedSessionKey, out _ );
        return _pocos.Create<ISessionOperationResult>( r => r.Cleared = true );
    }

    [CommandHandler]
    public virtual ISessionOperationResult Handle( IImpersonateOperationCommand op )
    {
        LastImpersonate = op;
        var cleared = Cleared( op, out var session, out var actualActor, out _ );
        if( cleared != null ) return cleared;
        var target = op.TargetActorId.HasValue ? _actors.Find( op.TargetActorId.Value ) : _actors.Find( op.TargetActorName ?? "" );
        if( target == null || target.IsWeak ) return Refuse( "Unknown actor.", "Test.UnknownActor" );
        return Issue( op, actualActor!, target, session!.RememberMe, TimeSpan.Zero );
    }

    [CommandHandler]
    public virtual ISessionOperationResult Handle( IClearImpersonationOperationCommand op )
    {
        var cleared = Cleared( op, out var session, out var actualActor, out _ );
        if( cleared != null ) return cleared;
        return Issue( op, actualActor!, actualActor!, session!.RememberMe, TimeSpan.Zero );
    }

    [CommandHandler]
    public virtual IList<ILoginSchemeInfo> Handle( IGetLoginSchemesQCommand cmd )
    {
        var result = new List<ILoginSchemeInfo>();
        foreach( var (name, critical) in LoginSchemes )
        {
            result.Add( _pocos.Create<ILoginSchemeInfo>( s => { s.Name = name; s.CriticalLifetime = critical; } ) );
        }
        return result;
    }

    [CommandHandler]
    public virtual RedirectedUrl Handle( IStartRemoteLoginOperationCommand op )
    {
        LastStartRemote = op;
        string? bindingSessionKey = null;
        if( op.Login!.BindToCurrentActor )
        {
            // The proof of the actor: a Critical session, not impersonated.
            if( op.PresentedSessionKey == null
                || !_sessions.TryGetValue( op.PresentedSessionKey, out var session )
                || session.CriticalExpires <= DateTime.UtcNow
                || session.ActualActorId != session.ActorId )
            {
                throw new MCException( MCString.CreateNonTranslatable( NormalizedCultureInfo.CodeDefault,
                                                                       "A binding requires a recent login (Critical level), not impersonated." ) );
            }
            bindingSessionKey = op.PresentedSessionKey;
        }
        var state = Guid.NewGuid().ToString( "N" );
        _remoteLogins[state] = new RemoteLogin( op.CorrelationHash, bindingSessionKey );
        // A Critical login asks the provider to authenticate the user again (the state stays the last parameter).
        return new RedirectedUrl( "https://idp.example.com/authorize?" + (op.Login!.Critical ? "prompt=login&" : "") + "state=" + state, op.Login.InlineMode );
    }

    [CommandHandler]
    public virtual ISessionOperationResult Handle( ICompleteRemoteLoginOperationCommand op )
    {
        LastCompleteRemote = op;
        var callback = op.Callback;
        if( callback == null
            || !_remoteLogins.TryRemove( callback.State, out var pending )
            || pending.CorrelationHash != op.PresentedCorrelationHash
            || callback.Code != RemoteLoginCode )
        {
            return Refuse( "Refused.", "Test.Refused" );
        }
        if( pending.BindingSessionKey != null )
        {
            // A binding: the session of the actor must still be alive; no session is issued.
            if( !_sessions.TryGetValue( pending.BindingSessionKey, out var session ) )
            {
                return Refuse( "The session that started the binding has ended.", "Test.Refused" );
            }
            Bindings.Enqueue( (LastStartRemote?.Login?.Scheme ?? "", session.ActorId) );
            return _pocos.Create<ISessionOperationResult>( r => r.Result = _pocos.Create<ISessionResult>( s => s.Success = true ) );
        }
        var a = _actors.Find( RemoteLoginActorId );
        return a != null ? Issue( op, a, a, rememberMe: true, TimeSpan.Zero ) : Refuse( "Unknown actor.", "Test.UnknownActor" );
    }

    // The passkey ceremonies: the browser's part is mocked by the client, these fakes check what it sends.

    [CommandHandler]
    public virtual string Handle( IStartPasskeyLoginOperationCommand op )
        => """{"challenge":"login-challenge","rpId":"localhost","allowCredentials":[],"userVerification":"required"}""";

    [CommandHandler]
    public virtual ISessionOperationResult Handle( IFinishPasskeyLoginOperationCommand op )
    {
        var login = op.Login;
        if( login == null || !login.Response.Contains( "\"signed\":\"login-challenge\"" ) )
        {
            return Refuse( "The passkey cannot be verified.", "Authentication.PasskeyVerificationFailed" );
        }
        var a = _actors.Find( PasskeyActorId );
        return a != null ? Issue( op, a, a, login.RememberMe, TimeSpan.FromMinutes( 5 ) ) : Refuse( "Unknown actor.", "Test.UnknownActor" );
    }

    [CommandHandler]
    public virtual string Handle( IStartPasskeyRegistrationOperationCommand op )
    {
        LastStartRegistration = op;
        return """{"challenge":"registration-challenge","rp":{"id":"localhost","name":"Test"},"user":{"id":"dXNlcg","name":"Albert","displayName":"Albert"},"pubKeyCredParams":[{"type":"public-key","alg":-7}]}""";
    }

    [CommandHandler]
    public virtual IPasskeyRegistrationResult Handle( IFinishPasskeyRegistrationOperationCommand op )
    {
        LastFinishRegistration = op;
        return op.CreateResult( r => r.Name = "Test passkey" );
    }
}
