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

            public void HandleCommand( ICmdTest cmd )
            {
                Called = true;
            }
        }

        public class CmdRefAsyncHandler : IAutoService
        {
            public Task HandleCommandAsync( ICmdTest cmd )
            {
                CmdSyncHandler.Called = true;
                return Task.CompletedTask;
            }
        }

        public class CmdValAsyncHandler : IAutoService
        {
            public ValueTask HandleCommandAsync( ICmdTest cmd )
            {
                CmdSyncHandler.Called = true;
                return default; // Should be ValueTask.CompletedTask one day: https://github.com/dotnet/runtime/issues/27960
            }
        }

        [TestCase( "Sync" )]
        [TestCase( "RefAsync" )]
        [TestCase( "ValAsync" )]
        public async Task basic_handling( string kind )
        {
            var c = TestHelper.CreateStObjCollector( typeof( FrontCommandExecutor ), typeof( CommandDirectory ), typeof( ICmdTest ) );
            c.RegisterType( kind switch
            {
                "RefASync" => typeof( CmdRefAsyncHandler ),
                "ValAsync" => typeof( CmdValAsyncHandler ),
                _ => typeof( CmdSyncHandler )
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
    }
}
