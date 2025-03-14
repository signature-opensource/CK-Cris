using CK.Auth;
using CK.Core;
using CK.Cris.AmbientValues;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.Executor.Tests;

[TestFixture]
public class CollectAmbientValuesTests
{
    /// <summary>
    /// Defines a set of ambient values that will be filled by the pseudo <see cref="AuthService"/> below.
    /// </summary>
    public interface IAuthAmbientValues : IAmbientValues
    {
        int ActorId { get; set; }
        int ActualActorId { get; set; }
        string DeviceId { get; set; }
    }

    /// <summary>
    /// Mimics the CK.Auth.Cris.CrisAuthenticationService (only the ambient values part, not the validation methods).
    /// </summary>
    public class AuthService : IAutoService
    {
        [CommandPostHandler]
        public void GetValues( IAmbientValuesCollectCommand cmd, IAuthenticationInfo info, IAuthAmbientValues values )
        {
            values.ActorId = info.User.UserId;
            values.ActualActorId = info.ActualUser.UserId;
            values.DeviceId = info.DeviceId;
        }
    }

    /// <summary>
    /// Another example: exposes a set of roles.
    /// </summary>
    public interface ISecurityAmbientValues : IAmbientValues
    {
        string[] Roles { get; set; }
    }


    /// <summary>
    /// Mimics a service that will retrieve roles from a database for the current user (this uses an async handler).
    /// Here also, a real service should also validate one or more command part that corresponds to the ambient values.
    /// </summary>
    public class SecurityService : IAutoService
    {
        [CommandPostHandler]
        public async Task GetValuesAsync( IAmbientValuesCollectCommand cmd, IActivityMonitor monitor, IAuthenticationInfo info, ISecurityAmbientValues values )
        {
            await Task.Delay( 25 );
            monitor.Info( $"User {info.User.UserName} roles have been read from the database." );
            values.Roles = new[] { "Administrator", "Tester", "Approver" };
        }
    }

    [Test]
    public async Task CommandPostHandler_fills_the_resulting_ambient_values_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                              typeof( IAmbientValuesCollectCommand ),
                                              typeof( AmbientValuesService ),
                                              typeof( AuthService ),
                                              typeof( IAuthenticationInfo ),
                                              typeof( StdAuthenticationTypeSystem ),
                                              typeof( IAuthAmbientValues ),
                                              typeof( SecurityService ),
                                              typeof( ISecurityAmbientValues ) );

        var authTypeSystem = new StdAuthenticationTypeSystem();
        var authInfo = authTypeSystem.AuthenticationInfo.Create( authTypeSystem.UserInfo.Create( 3712, "John" ), DateTime.UtcNow.AddDays( 1 ) );

        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices( configureServices: services =>
        {
            services.AddScoped<IAuthenticationInfo>( s => authInfo );
            services.AddScoped<IActivityMonitor>( s => TestHelper.Monitor );
        } );

        using( var scope = auto.Services.CreateScope() )
        {
            var services = scope.ServiceProvider;
            var executor = services.GetRequiredService<RawCrisExecutor>();
            var cmd = services.GetRequiredService<IPocoFactory<IAmbientValuesCollectCommand>>().Create();

            var r = await executor.RawExecuteAsync( services, cmd );
            Throw.DebugAssert( r.Result != null );
            var auth = (IAuthAmbientValues)r.Result;
            auth.ActorId.ShouldBe( 3712 );
            auth.ActualActorId.ShouldBe( 3712 );
            auth.DeviceId.ShouldBe( authInfo.DeviceId );

            var sec = (ISecurityAmbientValues)r.Result;
            sec.Roles.ShouldBe( "Administrator", "Tester", "Approver" );
        }
    }


    public interface ISomePart : ICrisPocoPart
    {
        [AmbientServiceValue]
        int? Something { get; set; }
    }

    public interface ISomeCommand : ICommand, ISomePart
    {
    }

    public class FakeHandlerButRequiredOtherwiseCommandIsSkipped
    {
        [CommandHandler]
        public void Handle( ISomeCommand command )
        {
        }
    }

    [Test]
    public async Task IAmbiantValues_must_cover_all_AmbientServiceValue_properties_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                              typeof( IAmbientValuesCollectCommand ),
                                              typeof( AmbientValuesService ),
                                              typeof( ISomePart ),
                                              typeof( ISomeCommand ),
                                              typeof( FakeHandlerButRequiredOtherwiseCommandIsSkipped ) );
        await configuration.GetFailedAutomaticServicesAsync(
            "Missing IAmbientValues properties for [AmbientServiceValue] properties.",
            new[] { "'int Something { get; set; }'" } );
    }

    public interface ICultureCommand : ICommandCurrentCulture
    {
    }

    // Required to test ConfigureAmbientService since for a non handled commands
    // validators, configurators and post handlers are trimmed out.
    public sealed class FakeCommandHandler : IAutoService
    {
        [CommandHandler]
        public void Handle( ICultureCommand command ) { }
    }

    [Test]
    public async Task configuring_AmbientServiceHub_Async()
    {
        NormalizedCultureInfo.EnsureNormalizedCultureInfo( "fr" );

        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ),
                                              typeof( RawCrisReceiver ),
                                              typeof( CrisCultureService ),
                                              typeof( NormalizedCultureInfo ),
                                              typeof( ICultureCommand ),
                                              typeof( FakeCommandHandler ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        auto.Services.GetRequiredService<IEnumerable<IHostedService>>().Count.ShouldBe( 1, "Required to initialize the Global Service Provider." );

        using( var scope = auto.Services.CreateScope() )
        {
            var s = scope.ServiceProvider;

            var poco = s.GetRequiredService<PocoDirectory>();
            var cmd = poco.Create<ICultureCommand>( c => c.CurrentCultureName = "fr" );

            var ambient = s.GetRequiredService<AmbientServiceHub>();
            ambient.GetCurrentValue<ExtendedCultureInfo>().ShouldBeSameAs( NormalizedCultureInfo.CodeDefault,
                "No global ConfigureServices, NormalizedCultureInfoUbiquitousServiceDefault has done its job." );

            var receiver = s.GetRequiredService<RawCrisReceiver>();
            var validationResult = await receiver.IncomingValidateAsync( TestHelper.Monitor, s, cmd );

            Throw.DebugAssert( validationResult.AmbientServiceHub != null );

            validationResult.AmbientServiceHub.GetCurrentValue<ExtendedCultureInfo>().Name.ShouldBe( "fr" );
        }
    }


}
