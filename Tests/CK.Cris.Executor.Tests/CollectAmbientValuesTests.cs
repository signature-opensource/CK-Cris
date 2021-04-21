using CK.Auth;
using CK.Core;
using CK.Cris.AmbientValues;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.Executor.Tests
{
    [TestFixture]
    public class CollectAmbientValuesTests
    {

        /// <summary>
        /// Mimics the CK.Auth.Cris.CrisAuthenticationService (only the ambient values part, not the validation methods).
        /// </summary>
        public class AuthService : IAutoService
        {
            [CommandPostHandler]
            public void GetValues( IAmbientValuesCollectCommand cmd, IAuthenticationInfo info, Dictionary<string, object?> values )
            {
                values.Add( "ActorId", info.User.UserId );
                values.Add( "ActualActorId", info.ActualUser.UserId );
                values.Add( "DeviceId", info.DeviceId );
            }
        }

        /// <summary>
        /// Mimics a service that will retrieve roles from a database for the current user (this uses an async handler).
        /// </summary>
        public class SecurityService : IAutoService
        {
            [CommandPostHandler]
            public async Task GetValuesAsync( IAmbientValuesCollectCommand cmd, IActivityMonitor monitor, IAuthenticationInfo info, Dictionary<string, object?> values )
            {
                await Task.Delay( 25 );
                monitor.Info( $"User {info.User.UserName} roles have been read from the database." );
                values.Add( "Roles", new[] { "Administrator", "Tester", "Approver" } );
            }
        }

        [Test]
        public async Task CommandPostHandler_fills_the_resulting_ambient_values()
        {
            var c = FrontCommandExecutorTests.CreateFrontCommandCollector( typeof( IAmbientValuesCollectCommand ), typeof( AmbientValuesCollectHandler ), typeof( AuthService ), typeof( SecurityService ) );

            var authTypeSystem = new StdAuthenticationTypeSystem();
            var authInfo = authTypeSystem.AuthenticationInfo.Create( authTypeSystem.UserInfo.Create( 3712, "John" ), DateTime.UtcNow.AddDays( 1 ) );
            var map = TestHelper.CompileAndLoadStObjMap( c ).Map;
            var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, new ServiceCollection() );
            reg.Register<IAuthenticationInfo>( s => authInfo, true, false );
            reg.AddStObjMap( map ).Should().BeTrue( "Service configuration succeed." );

            var appServices = reg.Services.BuildServiceProvider();

            using( var scope = appServices.CreateScope() )
            {
                var services = scope.ServiceProvider;
                var executor = services.GetRequiredService<FrontCommandExecutor>();
                var cmd = services.GetRequiredService<IPocoFactory<IAmbientValuesCollectCommand>>().Create();

                var r = await executor.ExecuteCommandAsync( TestHelper.Monitor, services, cmd );
                r.Code.Should().Be( VESACode.Synchronous );
                Debug.Assert( r.Result != null );
                var ambientValues = (Dictionary<string, object?>)r.Result!;
                ambientValues.Should().HaveCount( 4 );
            }
        }

    }
}
