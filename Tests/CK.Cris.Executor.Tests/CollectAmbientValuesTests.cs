using CK.Auth;
using CK.Core;
using CK.Cris.AmbientValues;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.Executor.Tests
{
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
            var c = TestHelper.CreateStObjCollector( typeof( RawCrisExecutor ),
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
            var map = TestHelper.CompileAndLoadStObjMap( c ).Map;
            var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, new ServiceCollection() );
            reg.Register<IAuthenticationInfo>( s => authInfo, isScoped: true, allowMultipleRegistration: false );
            reg.Register<IActivityMonitor>( s => TestHelper.Monitor, true, false );
            reg.AddStObjMap( map ).Should().BeTrue( "Service configuration succeed." );

            var appServices = reg.Services.BuildServiceProvider();

            using( var scope = appServices.CreateScope() )
            {
                var services = scope.ServiceProvider;
                var executor = services.GetRequiredService<RawCrisExecutor>();
                var cmd = services.GetRequiredService<IPocoFactory<IAmbientValuesCollectCommand>>().Create();

                var r = await executor.RawExecuteAsync( services, cmd );
                Throw.DebugAssert( r.Result != null );
                var auth = (IAuthAmbientValues)r.Result;
                auth.ActorId.Should().Be( 3712 );
                auth.ActualActorId.Should().Be( 3712 );
                auth.DeviceId.Should().Be( authInfo.DeviceId );

                var sec = (ISecurityAmbientValues)r.Result;
                sec.Roles.Should().BeEquivalentTo( "Administrator", "Tester", "Approver" );
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

        public class  FakeHandlerButRequiredOtherwiseCommandIsSkipped 
        {
            [CommandHandler]
            public void Handle( ISomeCommand command )
            {
            }
        }

        [Test]
        public void IAmbiantValues_must_cover_all_AmbientServiceValue_properties()
        {
            var c = TestHelper.CreateStObjCollector( typeof( RawCrisExecutor ),
                                                     typeof( IAmbientValuesCollectCommand ),
                                                     typeof( AmbientValuesService ),
                                                     typeof( ISomePart ),
                                                     typeof( ISomeCommand ),
                                                     typeof( FakeHandlerButRequiredOtherwiseCommandIsSkipped ) );
            TestHelper.GetFailedAutomaticServicesConfiguration( c, 
                "Missing IAmbientValues properties for [AmbientServiceValue] properties.",
                new[] { "'int Something { get; set; }'" } );
        }

        public interface ICultureCommand : ICommandWithCurrentCulture
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

            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ),
                                                     typeof( RawCrisReceiver ),
                                                     typeof( CrisCultureService ),
                                                     typeof( NormalizedCultureInfo ),
                                                     typeof( NormalizedCultureInfoUbiquitousServiceDefault ),
                                                     typeof( ICultureCommand ),
                                                     typeof( FakeCommandHandler ) );
            using var services = TestHelper.CreateAutomaticServices( c ).Services;
            services.GetRequiredService<IEnumerable<IHostedService>>().Should().HaveCount( 1, "Required to initialize the Global Service Provider." );

            using( var scope = services.CreateScope() )
            {
                var s = scope.ServiceProvider;

                var poco = s.GetRequiredService<PocoDirectory>();
                var cmd = poco.Create<ICultureCommand>( c => c.CurrentCultureName = "fr" );

                var ambient = s.GetRequiredService<AmbientServiceHub>();
                ambient.GetCurrentValue<ExtendedCultureInfo>().Should().BeSameAs( NormalizedCultureInfo.CodeDefault,
                    "No global ConfigureServices, NormalizedCultureInfoUbiquitousServiceDefault has done its job." );

                var receiver = s.GetRequiredService<RawCrisReceiver>();
                var validationResult = await receiver.IncomingValidateAsync( TestHelper.Monitor, s, cmd );

                Throw.DebugAssert( validationResult.AmbientServiceHub != null );

                validationResult.AmbientServiceHub.GetCurrentValue<ExtendedCultureInfo>().Name.Should().Be( "fr" );
            }
        }


    }
}
