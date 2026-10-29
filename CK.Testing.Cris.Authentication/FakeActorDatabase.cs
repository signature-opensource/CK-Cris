using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Testing;

/// <summary>
/// Fake actor database that contains "System" (1), "Alice" (3711), "Albert" (3712), "Robert" (3713), "Hubert" (3714)
/// and the weak actor "Operator" (42).
/// "Alice", "Albert" and "Hubert" have the "Basic" scheme (their password is <see cref="BasicPassword"/>).
/// "Hubert" also has the "Google" scheme.
/// <para>
/// The actors are at the highest level they can reach (<see cref="AuthLevel.Critical"/>, <see cref="AuthLevel.Unsafe"/>
/// for the weak actor): the session clamps them (see <see cref="IActorInfoProvider"/>).
/// </para>
/// <para>
/// <see cref="AllActors"/> and <see cref="Disabled"/> are totally mutable and everything is virtual.
/// </para>
/// </summary>
public class FakeActorDatabase : IActorInfoProvider
{
    /// <summary>
    /// The password of the actors that have the "Basic" scheme.
    /// </summary>
    public const string BasicPassword = "success";

    static readonly DateTime _lastUsed = new DateTime( 2000, 1, 1, 0, 0, 0, DateTimeKind.Utc );

    readonly List<ActorInfo> _actors;
    readonly HashSet<int> _disabled;

    /// <summary>
    /// Initializes a new database with its default actors.
    /// </summary>
    public FakeActorDatabase()
    {
        _actors =
        [
            new ActorInfo( 1, "System", AuthLevel.Critical ),
            new ActorInfo( 3711, "Alice", AuthLevel.Critical, [new SchemeInfo( "Basic", _lastUsed )] ),
            new ActorInfo( 3712, "Albert", AuthLevel.Critical, [new SchemeInfo( "Basic", _lastUsed )] ),
            new ActorInfo( 3713, "Robert", AuthLevel.Critical ),
            new ActorInfo( 3714, "Hubert", AuthLevel.Critical, [new SchemeInfo( "Basic", _lastUsed ), new SchemeInfo( "Google", _lastUsed )] ),
            new ActorInfo( 42, "Operator", AuthLevel.Unsafe, [new SchemeInfo( ActorInfo.WeakScheme, _lastUsed )] )
        ];
        _disabled = new HashSet<int>();
    }

    /// <summary>
    /// Gets all the actors.
    /// </summary>
    public virtual IList<ActorInfo> AllActors => _actors;

    /// <summary>
    /// Gets the identifiers of the disabled actors: they are not found anymore.
    /// </summary>
    public virtual ISet<int> Disabled => _disabled;

    /// <summary>
    /// Finds an actor that is not disabled by its identifier.
    /// </summary>
    /// <param name="actorId">The actor identifier.</param>
    /// <returns>The actor or null.</returns>
    public virtual ActorInfo? Find( int actorId )
    {
        return _disabled.Contains( actorId ) ? null : _actors.FirstOrDefault( a => a.ActorId == actorId );
    }

    /// <summary>
    /// Finds an actor that is not disabled by its name.
    /// </summary>
    /// <param name="actorName">The actor name.</param>
    /// <returns>The actor or null.</returns>
    public virtual ActorInfo? Find( string actorName )
    {
        var a = _actors.FirstOrDefault( a => a.ActorName == actorName );
        return a == null || _disabled.Contains( a.ActorId ) ? null : a;
    }

    /// <summary>
    /// The "Basic" login: the actor must have the "Basic" scheme and the password must be <see cref="BasicPassword"/>.
    /// </summary>
    /// <param name="userName">The actor name.</param>
    /// <param name="password">The password.</param>
    /// <returns>The actor or null.</returns>
    public virtual ActorInfo? BasicLogin( string userName, string password )
    {
        var a = Find( userName );
        return a != null && a.HasScheme( "Basic" ) && password == BasicPassword ? a : null;
    }

    /// <summary>
    /// The "Weak" login: a passwordless login of any actor that is not disabled (the weak actor "Operator", or any other
    /// one: a test convenience to open the session of an actor).
    /// </summary>
    /// <param name="weakActorName">The actor name.</param>
    /// <returns>The actor or null.</returns>
    public virtual ActorInfo? WeakLogin( string weakActorName ) => Find( weakActorName );

    /// <inheritdoc />
    public virtual ValueTask<ActorInfo?> GetActorInfoAsync( IActivityMonitor monitor, int actorId ) => ValueTask.FromResult( Find( actorId ) );
}
