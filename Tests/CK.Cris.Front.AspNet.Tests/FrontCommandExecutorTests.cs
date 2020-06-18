using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

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
                return Task.FromResult(3712);
            }
        }

        public class CmdIntValAsyncHandler : IAutoService
        {
            [CommandHandler]
            public ValueTask<int> HandleCommandAsync( ICmdIntTest cmd )
            {
                CmdIntSyncHandler.Called = true;
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


    }
}
