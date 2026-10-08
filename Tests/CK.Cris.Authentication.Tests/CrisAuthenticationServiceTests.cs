using CK.Auth;
using CK.Core;
using CK.Cris.AmbientValues;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.Authentication.Tests;

[TestFixture]
public class CrisAuthenticationServiceTests
{
    static readonly ActorInfo _albert = new ActorInfo( 3712, "Albert", AuthLevel.Critical );
    static readonly ActorInfo _robert = new ActorInfo( 12, "Robert", AuthLevel.Critical );
    static readonly ActorInfo _operator = new ActorInfo( 42, "Operator", AuthLevel.Unsafe, [new SchemeInfo( ActorInfo.WeakScheme, new DateTime( 2000, 1, 1, 0, 0, 0, DateTimeKind.Utc ) )] );

    // The endpoint authentication used by the scoped AuthenticationInfo.
    static AuthenticationInfo _current = AuthenticationInfo.None;

    [AllowNull]
    AutomaticServices _auto;

    public sealed class FakeActorInfoProvider : IActorInfoProvider
    {
        public ValueTask<ActorInfo?> GetActorInfoAsync( IActivityMonitor monitor, int actorId )
        {
            return ValueTask.FromResult( actorId switch { 3712 => _albert, 12 => _robert, 42 => _operator, _ => (ActorInfo?)null } );
        }
    }

    public interface IUnsafeCommand : ICommand<string>, ICommandAuthUnsafe { }

    public interface INormalCommand : ICommand<string>, ICommandAuthNormal { }

    public interface ICriticalCommand : ICommand<string>, ICommandAuthCritical { }

    public interface IImpersonatedCommand : ICommand<string>, ICommandAuthNormal, ICommandAuthImpersonation, ICommandAuthDeviceId { }

    public interface IImpersonatedUnsafeCommand : ICommand<string>, ICommandAuthImpersonation { }

    public sealed class Handlers : ISingletonAutoService
    {
        [CommandHandler]
        public string Handle( IUnsafeCommand cmd, AuthenticationInfo info ) => Describe( info );

        [CommandHandler]
        public string Handle( INormalCommand cmd, AuthenticationInfo info ) => Describe( info );

        [CommandHandler]
        public string Handle( ICriticalCommand cmd, AuthenticationInfo info ) => Describe( info );

        [CommandHandler]
        public string Handle( IImpersonatedCommand cmd, AuthenticationInfo info ) => Describe( info );

        [CommandHandler]
        public string Handle( IImpersonatedUnsafeCommand cmd, AuthenticationInfo info ) => Describe( info );

        static string Describe( AuthenticationInfo info )
            => $"{info.UnsafeActor.ActorId}/{info.UnsafeActor.Level}|{info.UnsafeActualActor.ActorId}/{info.UnsafeActualActor.Level}|{info.DeviceId}";
    }

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisAuthenticationService ),
                                              typeof( AuthenticationInfo ),
                                              typeof( AuthenticationInfoDefaultProvider ),
                                              typeof( FakeActorInfoProvider ),
                                              typeof( IAuthAmbientValues ),
                                              typeof( IAmbientValuesCollectCommand ),
                                              typeof( AmbientValuesService ),
                                              typeof( RawCrisExecutor ),
                                              typeof( RawCrisReceiver ),
                                              typeof( CrisDirectory ),
                                              typeof( ICrisResultError ),
                                              typeof( CrisBackgroundExecutorService ),
                                              typeof( IUnsafeCommand ),
                                              typeof( INormalCommand ),
                                              typeof( ICriticalCommand ),
                                              typeof( IImpersonatedCommand ),
                                              typeof( IImpersonatedUnsafeCommand ),
                                              typeof( Handlers ) );
        _auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices( configureServices: services =>
        {
            services.AddScoped( s => _current );
        } );
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        await _auto.DisposeAsync();
    }

    static DateTime Tomorrow => DateTime.UtcNow.AddDays( 1 );

    // The scoped AuthenticationInfo is resolved once per DI scope: each session needs its own scope.
    AsyncServiceScope CreateScope( AuthenticationInfo session )
    {
        _current = session;
        return _auto.Services.CreateAsyncScope();
    }

    [Test]
    public async Task ambient_values_are_the_unsafe_identifiers_so_that_a_weak_actor_can_send_Unsafe_commands_Async()
    {
        var session = AuthenticationInfo.Create( _operator, Tomorrow, null, "D", DateTime.UtcNow );
        session.Actor.ShouldBeSameAs( ActorInfo.Anonymous, "The weak actor is Unsafe." );
        await using var scope = CreateScope( session );
        var services = scope.ServiceProvider;
        var executor = services.GetRequiredService<RawCrisExecutor>();
        var cmd = services.GetRequiredService<IPocoFactory<IAmbientValuesCollectCommand>>().Create();
        var r = await executor.RawExecuteAsync( services, cmd );
        var values = (IAuthAmbientValues)r.Result!;
        values.ActorId.ShouldBe( 42 );
        values.ActualActorId.ShouldBe( 42 );
        values.DeviceId.ShouldBe( "D" );

        var unsafeCommand = services.GetRequiredService<PocoDirectory>().Create<IUnsafeCommand>( c => c.ActorId = values.ActorId );
        var v = await services.GetRequiredService<RawCrisReceiver>().IncomingValidateAsync( TestHelper.Monitor, services, unsafeCommand );
        v.Success.ShouldBeTrue();
    }

    async Task<string[]> ValidateAsync( AuthenticationInfo session, Func<PocoDirectory, ICrisPoco> create )
    {
        await using var scope = CreateScope( session );
        var services = scope.ServiceProvider;
        var cmd = create( services.GetRequiredService<PocoDirectory>() );
        var v = await services.GetRequiredService<RawCrisReceiver>().IncomingValidateAsync( TestHelper.Monitor, services, cmd );
        return v.ValidationMessages.Where( m => m.Level == UserMessageLevel.Error ).Select( m => m.Text ).ToArray();
    }

    [Test]
    public async Task incoming_validation_uses_the_level_of_the_user_Async()
    {
        // A weak actor: Unsafe only.
        var weak = AuthenticationInfo.Create( _operator, Tomorrow, null, null, DateTime.UtcNow );
        (await ValidateAsync( weak, p => p.Create<IUnsafeCommand>( c => c.ActorId = 42 ) )).ShouldBeEmpty();
        (await ValidateAsync( weak, p => p.Create<INormalCommand>( c => c.ActorId = 42 ) ))
            .ShouldBe( ["Invalid authentication level: Normal authentication level required."] );

        // A Normal session.
        var normal = AuthenticationInfo.Create( _albert, Tomorrow, null, null, DateTime.UtcNow );
        (await ValidateAsync( normal, p => p.Create<INormalCommand>( c => c.ActorId = 3712 ) )).ShouldBeEmpty();
        (await ValidateAsync( normal, p => p.Create<ICriticalCommand>( c => c.ActorId = 3712 ) ))
            .ShouldBe( ["Invalid authentication level: Critical authentication level required."] );
        (await ValidateAsync( normal, p => p.Create<INormalCommand>( c => c.ActorId = 12 ) ))
            .ShouldBe( ["Invalid actor identifier: the provided identifier doesn't match the current authentication."] );
        (await ValidateAsync( normal, p => p.Create<INormalCommand>() ))
            .ShouldBe( ["Invalid property: ActorId cannot be null."] );

        // A Critical session.
        var critical = AuthenticationInfo.Create( _albert, Tomorrow, Tomorrow, null, DateTime.UtcNow );
        (await ValidateAsync( critical, p => p.Create<ICriticalCommand>( c => c.ActorId = 3712 ) )).ShouldBeEmpty();

        // The anonymous can send Unsafe commands with ActorId = 0.
        (await ValidateAsync( AuthenticationInfo.None, p => p.Create<IUnsafeCommand>( c => c.ActorId = 0 ) )).ShouldBeEmpty();
        (await ValidateAsync( AuthenticationInfo.None, p => p.Create<INormalCommand>( c => c.ActorId = 0 ) ))
            .ShouldBe( ["Invalid authentication level: Normal authentication level required."] );
    }

    [Test]
    public async Task an_administrator_impersonating_a_weak_actor_sends_Unsafe_commands_only_Async()
    {
        var s = AuthenticationInfo.Create( _albert, Tomorrow, Tomorrow, "D", DateTime.UtcNow ).Impersonate( _operator, DateTime.UtcNow );
        (await ValidateAsync( s, p => p.Create<IUnsafeCommand>( c => c.ActorId = 42 ) )).ShouldBeEmpty();
        (await ValidateAsync( s, p => p.Create<IImpersonatedCommand>( c => { c.ActorId = 42; c.ActualActorId = 3712; c.DeviceId = "D"; } ) ))
            .ShouldBe( ["Invalid authentication level: Normal authentication level required."] );
        (await ValidateAsync( s, p => p.Create<IImpersonatedUnsafeCommand>( c => { c.ActorId = 42; c.ActualActorId = 3712; } ) )).ShouldBeEmpty();
        (await ValidateAsync( s, p => p.Create<IImpersonatedUnsafeCommand>( c => { c.ActorId = 42; c.ActualActorId = 12; } ) ))
            .ShouldBe( ["Invalid actual actor identifier: the provided identifier doesn't match the current authentication."] );
    }

    // Submits the command without ambient services: the authentication is restored from the command.
    async Task<string> ExecuteRestoredAsync<T>( Func<PocoDirectory, T> create ) where T : class, IAbstractCommand
    {
        await using var scope = CreateScope( AuthenticationInfo.None );
        var services = scope.ServiceProvider;
        var back = services.GetRequiredService<CrisBackgroundExecutorService>();
        var executed = await back.Submit( TestHelper.Monitor, create( services.GetRequiredService<PocoDirectory>() ), null ).ExecutedCommand;
        return executed.Result is ICrisResultError e
                ? "Error: " + e.Errors.Select( m => m.Text ).Concatenate( " - " )
                : (string)executed.Result!;
    }

    [Test]
    public async Task authentication_is_restored_from_the_command_at_the_level_required_by_its_parts_Async()
    {
        (await ExecuteRestoredAsync( p => p.Create<INormalCommand>( c => c.ActorId = 3712 ) ))
            .ShouldBe( "3712/Normal|3712/Normal|" );
        (await ExecuteRestoredAsync( p => p.Create<ICriticalCommand>( c => c.ActorId = 3712 ) ))
            .ShouldBe( "3712/Critical|3712/Critical|" );
        (await ExecuteRestoredAsync( p => p.Create<IUnsafeCommand>( c => c.ActorId = 42 ) ))
            .ShouldBe( "42/Unsafe|42/Unsafe|" );
        (await ExecuteRestoredAsync( p => p.Create<IUnsafeCommand>( c => c.ActorId = 0 ) ))
            .ShouldBe( "0/None|0/None|" );
        (await ExecuteRestoredAsync( p => p.Create<IImpersonatedCommand>( c => { c.ActorId = 12; c.ActualActorId = 3712; c.DeviceId = "D"; } ) ))
            .ShouldBe( "12/Normal|3712/Normal|D" );
        (await ExecuteRestoredAsync( p => p.Create<IImpersonatedUnsafeCommand>( c => { c.ActorId = 42; c.ActualActorId = 3712; } ) ))
            .ShouldBe( "42/Unsafe|3712/Unsafe|" );
    }

    [Test]
    public async Task restoration_refuses_unknown_users_Async()
    {
        (await ExecuteRestoredAsync( p => p.Create<INormalCommand>( c => c.ActorId = 999 ) )).ShouldStartWith( "Error: " );
        (await ExecuteRestoredAsync( p => p.Create<IImpersonatedUnsafeCommand>( c => { c.ActorId = 12; c.ActualActorId = 999; } ) )).ShouldStartWith( "Error: " );
    }
}
