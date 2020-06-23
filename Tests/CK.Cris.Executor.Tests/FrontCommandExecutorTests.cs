using CK.Auth;
using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

#nullable enable

namespace CK.Cris.Front.AspNet.Tests
{
    [TestFixture]
    public class FrontCommandExecutorTests
    {
        public interface ICmdTest : ICommand
        {
        }

        public class CmdSyncHandler : IAutoService
        {
            public static bool Called;

            [CommandHandler]
            public void HandleCommand( ICmdTest cmd )
            {
                Called = true;
            }
        }

        public class CmdRefAsyncHandler : IAutoService
        {
            [CommandHandler]
            public Task HandleCommandAsync( ICmdTest cmd )
            {
                CmdSyncHandler.Called = true;
                return Task.CompletedTask;
            }
        }

        public class CmdValAsyncHandler : IAutoService
        {
            [CommandHandler]
            public ValueTask HandleCommandAsync( ICmdTest cmd )
            {
                CmdSyncHandler.Called = true;
                return default; // Should be ValueTask.CompletedTask one day: https://github.com/dotnet/runtime/issues/27960
            }
        }

        [TestCase( "Sync" )]
        [TestCase( "RefAsync" )]
        [TestCase( "ValAsync" )]
        public async Task basic_handling_of_void_returns( string kind )
        {
            var c = TestHelper.CreateStObjCollector( typeof( FrontCommandExecutor ), typeof( CommandDirectory ), typeof( ICmdTest ) );
            c.RegisterType( kind switch
            {
                "RefAsync" => typeof( CmdRefAsyncHandler ),
                "ValAsync" => typeof( CmdValAsyncHandler ),
                "Sync" => typeof( CmdSyncHandler ),
                _ => throw new NotImplementedException()
            } );

            var appServices = TestHelper.GetAutomaticServices( c ).Services;
            using( var scope = appServices.CreateScope() )
            {
                var services = scope.ServiceProvider;
                var executor = services.GetRequiredService<FrontCommandExecutor>();
                var cmd = services.GetRequiredService<IPocoFactory<ICmdTest>>().Create();

                CmdSyncHandler.Called = false;

                CommandResult result = await executor.ExecuteCommandAsync( TestHelper.Monitor, services, cmd );
                result.Caller.Should().BeNull();
                result.Result.Should().BeNull();
                result.EndExecutionTime.Should().NotBeNull();
                result.Code.Should().Be( VISAMCode.Synchronous );
                CmdSyncHandler.Called.Should().BeTrue();
            }
        }

        public interface ICmdIntTest : ICommand<int>
        {
        }

        public class CmdIntSyncHandler : IAutoService
        {
            public static bool Called;

            [CommandHandler]
            public int HandleCommand( ICmdIntTest cmd )
            {
                Called = true;
                return 3712;
            }
        }

        public class CmdIntRefAsyncHandler : IAutoService
        {
            [CommandHandler]
            public Task<int> HandleCommandAsync( ICmdIntTest cmd )
            {
                CmdIntSyncHandler.Called = true;
                return Task.FromResult( 3712 );
            }
        }

        public class CmdIntValAsyncHandler : IAutoService
        {
            public static bool Called;

            [CommandHandler]
            public ValueTask<int> HandleCommandAsync( ICmdIntTest cmd )
            {
                CmdIntValAsyncHandler.Called = CmdIntSyncHandler.Called = true;
                return new ValueTask<int>( 3712 );
            }
        }

        [TestCase( "Sync" )]
        [TestCase( "RefAsync" )]
        [TestCase( "ValAsync" )]
        public async Task basic_handling_with_returned_type( string kind )
        {
            var c = TestHelper.CreateStObjCollector( typeof( FrontCommandExecutor ), typeof( CommandDirectory ), typeof( ICmdIntTest ) );
            c.RegisterType( kind switch
            {
                "RefAsync" => typeof( CmdIntRefAsyncHandler ),
                "ValAsync" => typeof( CmdIntValAsyncHandler ),
                "Sync" => typeof( CmdIntSyncHandler ),
                _ => throw new NotImplementedException()
            } );

            var appServices = TestHelper.GetAutomaticServices( c ).Services;
            using( var scope = appServices.CreateScope() )
            {
                var services = scope.ServiceProvider;
                var executor = services.GetRequiredService<FrontCommandExecutor>();
                var cmd = services.GetRequiredService<IPocoFactory<ICmdIntTest>>().Create();

                CmdIntSyncHandler.Called = false;

                CommandResult result = await executor.ExecuteCommandAsync( TestHelper.Monitor, services, cmd );
                result.Caller.Should().BeNull();
                result.Result.Should().Be( 3712 );
                result.EndExecutionTime.Should().NotBeNull();
                result.Code.Should().Be( VISAMCode.Synchronous );
                CmdIntSyncHandler.Called.Should().BeTrue();
            }
        }

        [Test]
        public void ambiguous_handler_detection()
        {
            var c = TestHelper.CreateStObjCollector( typeof( FrontCommandExecutor ), typeof( CommandDirectory ), typeof( ICmdIntTest ), typeof( CmdIntRefAsyncHandler ), typeof( CmdIntValAsyncHandler ) );
            TestHelper.GenerateCode( c ).CodeGenResult.Success.Should().BeFalse();
        }

        public class CmdIntValAsyncHandlerService : CmdIntValAsyncHandler, ICommandHandler<ICmdIntTest>
        {
        }


        [Test]
        public void ambiguous_handler_resolution_thanks_to_the_ICommanHandlerT_marker()
        {
            CmdIntValAsyncHandler.Called = false;
            var c = TestHelper.CreateStObjCollector( typeof( FrontCommandExecutor ), typeof( CommandDirectory ), typeof( ICmdIntTest ), typeof( CmdIntRefAsyncHandler ), typeof( CmdIntValAsyncHandlerService ) );
            TestHelper.GenerateCode( c ).CodeGenResult.Success.Should().BeTrue();
        }


        public class CmdIntSyncHandlerWithBadReturnType1 : IAutoService
        {
            [CommandHandler]
            public string HandleCommand( ICmdIntTest cmd )
            {
                return "Won't compile.";
            }
        }

        public class CmdIntSyncHandlerWithBadReturnType2 : IAutoService
        {
            [CommandHandler]
            public void HandleCommand( ICmdIntTest cmd )
            {
                // Won't compile
            }
        }

        public class CmdIntSyncHandlerWithBadReturnType3 : IAutoService
        {
            [CommandHandler]
            public object HandleCommand( ICmdIntTest cmd )
            {
                return "Won't compile, even if object generalize int: the EXACT result type must be returned.";
            }
        }

        [Test]
        public void return_type_mismatch_detection()
        {
            {
                var c = TestHelper.CreateStObjCollector( typeof( FrontCommandExecutor ), typeof( CommandDirectory ), typeof( ICmdIntTest ), typeof( CmdIntSyncHandlerWithBadReturnType1 ) );
                CheckUniqueCommandHasNoHandler( c );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( FrontCommandExecutor ), typeof( CommandDirectory ), typeof( ICmdIntTest ), typeof( CmdIntSyncHandlerWithBadReturnType2 ) );
                CheckUniqueCommandHasNoHandler( c );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( FrontCommandExecutor ), typeof( CommandDirectory ), typeof( ICmdIntTest ), typeof( CmdIntSyncHandlerWithBadReturnType3 ) );
                CheckUniqueCommandHasNoHandler( c );
            }

            static void CheckUniqueCommandHasNoHandler( Setup.StObjCollector c )
            {
                var appServices = TestHelper.GetAutomaticServices( c ).Services;
                using( var scope = appServices.CreateScope() )
                {
                    var directory = scope.ServiceProvider.GetRequiredService<CommandDirectory>();
                    directory.Commands[0].Handler.Should().BeNull();
                }
            }
        }

        #region CollectAmbientValues Handler & PostHandler.
        public interface ICollectAmbientValuesCmd : ICommand<Dictionary<string, object?>>
        {
        }

        public class AmbientValuesService : IAutoService
        {
            [CommandHandler]
            public Dictionary<string, object?> GetValues( ICollectAmbientValuesCmd cmd ) => new Dictionary<string, object?>();
        }

        public class AuthService : IAutoService
        {
            [CommandPostHandler]
            public void GetValues( ICollectAmbientValuesCmd cmd, IAuthenticationInfo info, Dictionary<string, object?> values )
            {
                values.Add( "ActorId", info.User.UserId );
                values.Add( "ActualActorId", info.ActualUser.UserId );
            }
        }

        public class SecurityService : IAutoService
        {
            [CommandPostHandler]
            public async Task GetValuesAsync( ICollectAmbientValuesCmd cmd, IActivityMonitor monitor, IAuthenticationInfo info, Dictionary<string, object?> values )
            {
                await Task.Delay( 25 );
                monitor.Info( $"User {info.User.UserName} roles have been read from the database." );
                values.Add( "Roles", new[] { "Admin", "Tester", "Approver" } );
            }
        }

        [Test]
        public async Task calling_PostHandlers()
        {
            var c = TestHelper.CreateStObjCollector( typeof( FrontCommandExecutor ), typeof( CommandDirectory ), typeof( ICollectAmbientValuesCmd ), typeof( AmbientValuesService ), typeof( AuthService ), typeof( SecurityService ) );

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
                var cmd = services.GetRequiredService<IPocoFactory<ICollectAmbientValuesCmd>>().Create();

                var r = await executor.ExecuteCommandAsync( TestHelper.Monitor, services, cmd );
                r.Code.Should().Be( VISAMCode.Synchronous );
                Debug.Assert( r.Result != null );
                var ambientValues = (Dictionary<string, object?>)r.Result!;
                ambientValues.Should().HaveCount( 3 );
            }
        }
        #endregion


        #region [CommandHandler] on IAutoService publicly implemented.

        public interface ICommandHandler : IAutoService
        {
            [CommandHandler]
            void Handle( IActivityMonitor m, ICmdTest c );
        }

        public class CommandHandlerImpl : ICommandHandler
        {
            public static bool Called;

            public void Handle( IActivityMonitor m, ICmdTest c )
            {
                Called = true;
                m.Info( "Hop!" );
            }
        }

        [Test]
        public async Task calling_public_AutoService_implementation()
        {
            var c = TestHelper.CreateStObjCollector( typeof( FrontCommandExecutor ), typeof( CommandDirectory ), typeof( ICmdTest ), typeof( CommandHandlerImpl ) );
            var appServices = TestHelper.GetAutomaticServices( c ).Services;
            using( var scope = appServices.CreateScope() )
            {
                var services = scope.ServiceProvider;
                var executor = services.GetRequiredService<FrontCommandExecutor>();
                var cmd = services.GetRequiredService<IPocoFactory<ICmdTest>>().Create();

                CommandHandlerImpl.Called = false;

                CommandResult result = await executor.ExecuteCommandAsync( TestHelper.Monitor, services, cmd );
                result.Result.Should().BeNull();
                result.Code.Should().Be( VISAMCode.Synchronous );

                CommandHandlerImpl.Called.Should().BeTrue();
            }
        }

        #endregion


        #region [CommandHandler] on IAutoService explicitly implemented.

        public class CommandHandlerExplicitImpl : ICommandHandler
        {
            public static bool Called;

            void ICommandHandler.Handle( IActivityMonitor m, ICmdTest c )
            {
                Called = true;
                m.Info( "Explicit Hop!" );
            }
        }

        [Test]
        public async Task calling_explicit_AutoService_implementation()
        {
            var c = TestHelper.CreateStObjCollector( typeof( FrontCommandExecutor ), typeof( CommandDirectory ), typeof( ICmdTest ), typeof( CommandHandlerExplicitImpl ) );
            var appServices = TestHelper.GetAutomaticServices( c ).Services;
            using( var scope = appServices.CreateScope() )
            {
                var services = scope.ServiceProvider;
                var executor = services.GetRequiredService<FrontCommandExecutor>();
                var cmd = services.GetRequiredService<IPocoFactory<ICmdTest>>().Create();

                CommandHandlerExplicitImpl.Called = false;

                CommandResult result = await executor.ExecuteCommandAsync( TestHelper.Monitor, services, cmd );
                result.Result.Should().BeNull();
                result.Code.Should().Be( VISAMCode.Synchronous );

                CommandHandlerExplicitImpl.Called.Should().BeTrue();
            }
        }

        #endregion


    }
}
