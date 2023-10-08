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
            var c = RawCrisExecutorCommandTests.CreateRawExecutorCollector( typeof( IAmbientValuesCollectCommand ),
                                                                           typeof( AmbientValuesService ),
                                                                           typeof( AuthService ),
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
                Throw.DebugAssert( r != null );
                var auth = (IAuthAmbientValues)r;
                auth.ActorId.Should().Be( 3712 );
                auth.ActualActorId.Should().Be( 3712 );
                auth.DeviceId.Should().Be( authInfo.DeviceId );

                var sec = (ISecurityAmbientValues)r;
                sec.Roles.Should().BeEquivalentTo( "Administrator", "Tester", "Approver" );
            }
        }

    }
}
