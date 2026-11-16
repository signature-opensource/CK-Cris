using CK.Core;
using CK.IO.Authentication.Basic;
using CK.IO.Authentication.Weak;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Testing;

/// <summary>
/// The "Basic" login provider of a real authentication center, on the <see cref="FakeActorDatabase"/>: its payload is the
/// <see cref="IBasicLoginCommand"/> (see <see cref="FakeActorDatabase.BasicLogin(string, string)"/>). It records the last
/// password it received.
/// </summary>
public class FakeBasicLoginProvider : ILoginProvider
{
    readonly FakeActorDatabase _actors;

    /// <summary>
    /// Initializes a new provider.
    /// </summary>
    /// <param name="actors">The actors.</param>
    public FakeBasicLoginProvider( FakeActorDatabase actors ) => _actors = actors;

    /// <summary>
    /// Gets the last password received.
    /// </summary>
    public string? LastPassword { get; private set; }

    /// <inheritdoc />
    public IReadOnlyCollection<string> ProviderNames => ["Basic"];

    /// <inheritdoc />
    public object CreatePayload( string scheme ) => throw new ArgumentException( "No payload." );

    /// <inheritdoc />
    public virtual ValueTask<ActorLoginResult> LoginAsync( IActivityMonitor monitor, string scheme, object payload, bool actualLogin = true )
    {
        var cmd = (IBasicLoginCommand)payload;
        LastPassword = cmd.Password;
        var a = _actors.BasicLogin( cmd.UserName, cmd.Password );
        return ValueTask.FromResult( a != null ? ActorLoginResult.Success( a ) : ActorLoginResult.Failure( 4, "Invalid credentials." ) );
    }
}

/// <summary>
/// The "Weak" login provider of a real authentication center, on the <see cref="FakeActorDatabase"/>: its payload is the
/// <see cref="IWeakLoginCommand"/> (see <see cref="FakeActorDatabase.WeakLogin(string)"/>).
/// </summary>
public class FakeWeakLoginProvider : ILoginProvider
{
    readonly FakeActorDatabase _actors;

    /// <summary>
    /// Initializes a new provider.
    /// </summary>
    /// <param name="actors">The actors.</param>
    public FakeWeakLoginProvider( FakeActorDatabase actors ) => _actors = actors;

    /// <inheritdoc />
    public IReadOnlyCollection<string> ProviderNames => [ActorInfo.WeakScheme];

    /// <inheritdoc />
    public object CreatePayload( string scheme ) => throw new ArgumentException( "No payload." );

    /// <inheritdoc />
    public virtual ValueTask<ActorLoginResult> LoginAsync( IActivityMonitor monitor, string scheme, object payload, bool actualLogin = true )
    {
        var a = _actors.WeakLogin( ((IWeakLoginCommand)payload).WeakActorName );
        return ValueTask.FromResult( a != null ? ActorLoginResult.Success( a ) : ActorLoginResult.Failure( 3, "Invalid user name or identifier." ) );
    }
}

/// <summary>
/// The payload of the <see cref="FakeOidcLoginProvider"/> (like the IUserOidcInfo of CK.DB.User.UserOidc): the
/// OpenID Connect scheme family sets its <see cref="SchemeSuffix"/>, <see cref="Sub"/> and <see cref="Claims"/>.
/// </summary>
public sealed class FakeOidcPayload
{
    /// <summary>
    /// Gets or sets the suffix of the scheme ("Test" for "Oidc.Test").
    /// </summary>
    public string SchemeSuffix { get; set; } = "";

    /// <summary>
    /// Gets or sets the subject identifier at the provider.
    /// </summary>
    public string Sub { get; set; } = "";

    /// <summary>
    /// Gets or sets the claims.
    /// </summary>
    public string Claims { get; set; } = "";
}

/// <summary>
/// The "Oidc" login provider of a real authentication center, on the <see cref="FakeActorDatabase"/>: the identities
/// (scheme suffix, subject) bound to an actor are the <see cref="Bindings"/>. By default, in the "Test" scheme,
/// "albert-sub" is Albert (3712) and "robert-sub" is Robert (3713). It records the last payload.
/// </summary>
public class FakeOidcLoginProvider : ILoginProvider
{
    readonly FakeActorDatabase _actors;

    /// <summary>
    /// Initializes a new provider.
    /// </summary>
    /// <param name="actors">The actors.</param>
    public FakeOidcLoginProvider( FakeActorDatabase actors )
    {
        _actors = actors;
        Bindings = new Dictionary<(string SchemeSuffix, string Sub), int> { [("Test", "albert-sub")] = 3712, [("Test", "robert-sub")] = 3713 };
    }

    /// <summary>
    /// Gets the identities bound to an actor.
    /// </summary>
    public Dictionary<(string SchemeSuffix, string Sub), int> Bindings { get; }

    /// <summary>
    /// Gets the last payload received.
    /// </summary>
    public FakeOidcPayload? LastPayload { get; private set; }

    /// <inheritdoc />
    public IReadOnlyCollection<string> ProviderNames => ["Oidc"];

    /// <inheritdoc />
    public object CreatePayload( string scheme ) => new FakeOidcPayload();

    /// <inheritdoc />
    public virtual ValueTask<ActorLoginResult> LoginAsync( IActivityMonitor monitor, string scheme, object payload, bool actualLogin = true )
    {
        var p = (FakeOidcPayload)payload;
        LastPayload = p;
        var a = Bindings.TryGetValue( (p.SchemeSuffix, p.Sub), out var actorId ) ? _actors.Find( actorId ) : null;
        return ValueTask.FromResult( a != null
                                        ? ActorLoginResult.Success( a )
                                        : ActorLoginResult.Failure( 5, "No actor is bound to this identity.", isUnregisteredUser: true ) );
    }
}

/// <summary>
/// The automatic account binding of a real authentication center (the rules of CK.DB.Auth's SqlAutoBindingAccountService):
/// the identity of an "Oidc" login for which nobody is registered is bound to the logged in user (Critical level, not
/// impersonated) in the <see cref="FakeOidcLoginProvider.Bindings"/>. The other schemes are never bound.
/// </summary>
public class FakeAutoBindingAccountService : IAutoBindingAccountService
{
    readonly FakeOidcLoginProvider _oidc;
    readonly FakeActorDatabase _actors;

    /// <summary>
    /// Initializes a new binding service.
    /// </summary>
    /// <param name="oidc">The "Oidc" provider.</param>
    /// <param name="actors">The actors.</param>
    public FakeAutoBindingAccountService( FakeOidcLoginProvider oidc, FakeActorDatabase actors )
    {
        _oidc = oidc;
        _actors = actors;
    }

    /// <inheritdoc />
    public virtual ValueTask<ActorLoginResult?> BindAccountAsync( IActivityMonitor monitor, LoginContext context )
    {
        var auth = context.InitialAuthentication;
        if( auth.IsImpersonated )
        {
            return ValueTask.FromResult( context.SetError( "User.AccountBinding.Impersonated", "Account binding is not possible while impersonating." ) );
        }
        if( auth.UnsafeActor.Level != AuthLevel.Critical )
        {
            return ValueTask.FromResult( context.SetError( "User.AccountBinding.CriticalLevelRequired", "User must be logged in Critical level." ) );
        }
        if( context.Payload is not FakeOidcPayload p )
        {
            return ValueTask.FromResult( context.SetError( "User.AccountBinding.NotSupported", $"The '{context.Scheme}' scheme cannot be bound by a login." ) );
        }
        var a = _actors.Find( auth.UnsafeActor.ActorId );
        if( a == null ) return ValueTask.FromResult<ActorLoginResult?>( ActorLoginResult.Failure( 1, "Unknown actor." ) );
        _oidc.Bindings[(p.SchemeSuffix, p.Sub)] = a.ActorId;
        return ValueTask.FromResult<ActorLoginResult?>( ActorLoginResult.Success( a ) );
    }
}

/// <summary>
/// The passkey storage, in memory.
/// </summary>
public class InMemoryPasskeyStore : IPasskeyStore
{
    readonly Dictionary<string, PasskeyCredential> _credentials = new();
    readonly Dictionary<int, byte[]> _handles = new();

    /// <summary>
    /// Gets all the credentials.
    /// </summary>
    public IReadOnlyCollection<PasskeyCredential> All
    {
        get
        {
            lock( _credentials ) return _credentials.Values.ToList();
        }
    }

    static string Key( ReadOnlyMemory<byte> id ) => Convert.ToHexString( id.Span );

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> GetOrCreateUserHandleAsync( IActivityMonitor monitor, int actorId )
    {
        lock( _handles )
        {
            if( !_handles.TryGetValue( actorId, out var h ) ) _handles.Add( actorId, h = System.Security.Cryptography.RandomNumberGenerator.GetBytes( 32 ) );
            return ValueTask.FromResult<ReadOnlyMemory<byte>>( h );
        }
    }

    /// <inheritdoc />
    public ValueTask<PasskeyCredential?> FindAsync( IActivityMonitor monitor, ReadOnlyMemory<byte> credentialId )
    {
        lock( _credentials ) return ValueTask.FromResult( _credentials.GetValueOrDefault( Key( credentialId ) ) );
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<PasskeyCredential>> GetCredentialsAsync( IActivityMonitor monitor, int actorId )
    {
        lock( _credentials ) return ValueTask.FromResult<IReadOnlyList<PasskeyCredential>>( _credentials.Values.Where( c => c.ActorId == actorId ).ToList() );
    }

    /// <inheritdoc />
    public ValueTask<bool> CreateAsync( IActivityMonitor monitor, PasskeyCredential credential )
    {
        lock( _credentials ) return ValueTask.FromResult( _credentials.TryAdd( Key( credential.CredentialId ), credential ) );
    }

    /// <summary>
    /// Updates a credential after an actual login (what the "Passkey" provider does).
    /// </summary>
    /// <param name="p">The login payload.</param>
    public void Update( PasskeyLoginPayload p )
    {
        lock( _credentials )
        {
            var c = _credentials[Key( p.CredentialId )];
            _credentials[Key( p.CredentialId )] = new PasskeyCredential( c.CredentialId, c.ActorId, c.UserHandle, c.PublicKey, p.SignCount, c.BackupEligible, p.BackupState, c.Aaguid, c.Transports, c.Name );
        }
    }
}

/// <summary>
/// The "Passkey" login provider of a real authentication center, on the <see cref="InMemoryPasskeyStore"/> and the
/// <see cref="FakeActorDatabase"/>.
/// </summary>
public class FakePasskeyLoginProvider : ILoginProvider
{
    readonly InMemoryPasskeyStore _store;
    readonly FakeActorDatabase _actors;

    /// <summary>
    /// Initializes a new provider.
    /// </summary>
    /// <param name="store">The passkey store.</param>
    /// <param name="actors">The actors.</param>
    public FakePasskeyLoginProvider( InMemoryPasskeyStore store, FakeActorDatabase actors )
    {
        _store = store;
        _actors = actors;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> ProviderNames => [PasskeyLoginPayload.Scheme];

    /// <inheritdoc />
    public object CreatePayload( string scheme ) => throw new ArgumentException( "No payload." );

    /// <inheritdoc />
    public virtual async ValueTask<ActorLoginResult> LoginAsync( IActivityMonitor monitor, string scheme, object payload, bool actualLogin = true )
    {
        var p = (PasskeyLoginPayload)payload;
        var c = await _store.FindAsync( monitor, p.CredentialId );
        var a = c != null ? _actors.Find( c.ActorId ) : null;
        if( a == null ) return ActorLoginResult.Failure( 6, "Unknown passkey." );
        if( actualLogin ) _store.Update( p );
        return ActorLoginResult.Success( a );
    }
}
