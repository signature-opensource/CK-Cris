using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.Executor.Tests;


[TestFixture]
public class RawCrisExecutorCommandTests
{

    public interface ITestCommand : ICommand
    {
    }

    public class CmdSyncHandler : IAutoService
    {
        public static bool Called;

        [CommandHandler]
        public void HandleCommand( ITestCommand cmd )
        {
            Called = true;
        }
    }

    public class CmdRefAsyncHandler : IAutoService
    {
        [CommandHandler( AllowUnclosedCommand = true )]
        public Task HandleCommandAsync( ITestCommand cmd )
        {
            CmdSyncHandler.Called = true;
            return Task.CompletedTask;
        }
    }

    public class CmdValAsyncHandler : IAutoService
    {
        [CommandHandler]
        public ValueTask HandleCommandAsync( ITestCommand cmd )
        {
            CmdSyncHandler.Called = true;
            return ValueTask.CompletedTask;
        }
    }

    [TestCase( "Sync" )]
    [TestCase( "RefAsync" )]
    [TestCase( "ValAsync" )]
    public async Task basic_handling_of_void_returns_Async( string kind )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                              typeof( ITestCommand ),
                                              kind switch
                                              {
                                                  "RefAsync" => typeof( CmdRefAsyncHandler ),
                                                  "ValAsync" => typeof( CmdValAsyncHandler ),
                                                  "Sync" => typeof( CmdSyncHandler ),
                                                  _ => throw new NotImplementedException()
                                              } );

        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        using( var scope = auto.Services.CreateScope() )
        {
            var services = scope.ServiceProvider;
            var executor = services.GetRequiredService<RawCrisExecutor>();
            var cmd = services.GetRequiredService<IPocoFactory<ITestCommand>>().Create();

            CmdSyncHandler.Called = false;

            var raw = await executor.RawExecuteAsync( services, cmd );
            raw.Result.Should().BeNull();
            CmdSyncHandler.Called.Should().BeTrue();
        }
    }

    public interface IIntTestCommand : ICommand<int>
    {
    }

    public class CmdIntSyncHandler : IAutoService
    {
        public static bool Called;

        [CommandHandler]
        public int HandleCommand( IIntTestCommand cmd )
        {
            Called = true;
            return 3712;
        }
    }

    public class CmdIntRefAsyncHandler : IAutoService
    {
        [CommandHandler]
        public Task<int> HandleCommandAsync( IIntTestCommand cmd )
        {
            CmdIntSyncHandler.Called = true;
            return Task.FromResult( 3712 );
        }
    }

    public class CmdIntValAsyncHandler : IAutoService
    {
        public static bool Called;

        [CommandHandler]
        public ValueTask<int> HandleCommandAsync( IIntTestCommand cmd )
        {
            CmdIntValAsyncHandler.Called = CmdIntSyncHandler.Called = true;
            return new ValueTask<int>( 3712 );
        }
    }

    [TestCase( "Sync" )]
    [TestCase( "RefAsync" )]
    [TestCase( "ValAsync" )]
    public async Task basic_handling_with_returned_type_Async( string kind )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                              typeof( IIntTestCommand ),
                                              kind switch
                                              {
                                                  "RefAsync" => typeof( CmdIntRefAsyncHandler ),
                                                  "ValAsync" => typeof( CmdIntValAsyncHandler ),
                                                  "Sync" => typeof( CmdIntSyncHandler ),
                                                  _ => throw new NotImplementedException()
                                              } );

        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        using( var scope = auto.Services.CreateScope() )
        {
            var services = scope.ServiceProvider;
            var executor = services.GetRequiredService<RawCrisExecutor>();
            var cmd = services.GetRequiredService<IPocoFactory<IIntTestCommand>>().Create();

            CmdIntSyncHandler.Called = false;

            var raw = await executor.RawExecuteAsync( services, cmd );
            raw.Result.Should().Be( 3712 );
            CmdIntSyncHandler.Called.Should().BeTrue();
        }
    }

    [Test]
    public async Task ambiguous_handler_detection_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                              typeof( IIntTestCommand ),
                                              typeof( CmdIntRefAsyncHandler ),
                                              typeof( CmdIntValAsyncHandler ) );
        await configuration.GetFailedAutomaticServicesAsync(
            "Ambiguity: both 'CmdIntValAsyncHandler.HandleCommandAsync( IIntTestCommand cmd )' and 'CK.Cris.Executor.Tests.RawCrisExecutorCommandTests+CmdIntRefAsyncHandler.HandleCommandAsync' handle 'CK.Cris.Executor.Tests.RawCrisExecutorCommandTests.IIntTestCommand' command." );
    }

    public class CmdIntValAsyncHandlerService : CmdIntValAsyncHandler, ICommandHandler<IIntTestCommand>
    {
    }


    [Test]
    public async Task ambiguous_handler_resolution_thanks_to_the_ICommanHandlerT_marker_Async()
    {
        CmdIntValAsyncHandler.Called = false;

        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                              typeof( IIntTestCommand ),
                                              typeof( CmdIntRefAsyncHandler ),
                                              typeof( CmdIntValAsyncHandlerService ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
    }


    public class CmdIntSyncHandlerWithBadReturnType1 : IAutoService
    {
        [CommandHandler]
        public string HandleCommand( IIntTestCommand cmd )
        {
            return "Won't compile.";
        }
    }

    public class CmdIntSyncHandlerWithBadReturnType2 : IAutoService
    {
        [CommandHandler]
        public void HandleCommand( IIntTestCommand cmd )
        {
            // Won't compile
        }
    }

    public class CmdIntSyncHandlerWithBadReturnType3 : IAutoService
    {
        [CommandHandler]
        public object HandleCommand( IIntTestCommand cmd )
        {
            return "Won't compile, even if object generalize int: the EXACT result type must be returned.";
        }
    }

    [Test]
    public async Task return_type_mismatch_detection_Async()
    {
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                                  typeof( IIntTestCommand ),
                                                  typeof( CmdIntSyncHandlerWithBadReturnType1 ) );
            await CheckUniqueCommandHasNoHandlerAsync( configuration );
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                                  typeof( IIntTestCommand ),
                                                  typeof( CmdIntSyncHandlerWithBadReturnType2 ) );
            await CheckUniqueCommandHasNoHandlerAsync( configuration );
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                                  typeof( IIntTestCommand ),
                                                  typeof( CmdIntSyncHandlerWithBadReturnType3 ) );
            await CheckUniqueCommandHasNoHandlerAsync( configuration );
        }

        static async Task CheckUniqueCommandHasNoHandlerAsync( EngineConfiguration configuration )
        {
            await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
            using( var scope = auto.Services.CreateScope() )
            {
                var directory = scope.ServiceProvider.GetRequiredService<CrisDirectory>();
                directory.CrisPocoModels[0].Handlers.Should().BeEmpty();
            }
        }
    }

    #region [CommandHandler] on IAutoService publicly implemented.

    public interface ICommandHandler : IAutoService
    {
        [CommandHandler]
        void Handle( IActivityMonitor m, ITestCommand c );
    }

    public class CommandHandlerImpl : ICommandHandler
    {
        public static bool Called;

        public void Handle( IActivityMonitor m, ITestCommand c )
        {
            Called = true;
            m.Info( "Hop!" );
        }
    }

    [Test]
    public async Task calling_public_AutoService_implementation_Async()
    {
        await using var auto = await TestHelper.CreateAutomaticServicesWithMonitorAsync(
            [
                typeof( RawCrisExecutor ),
                typeof( ITestCommand ),
                typeof( CommandHandlerImpl )
            ] );
        using( var scope = auto.Services.CreateScope() )
        {
            var services = scope.ServiceProvider;
            var executor = services.GetRequiredService<RawCrisExecutor>();
            var cmd = services.GetRequiredService<IPocoFactory<ITestCommand>>().Create();

            CommandHandlerImpl.Called = false;

            var raw = await executor.RawExecuteAsync( services, cmd );
            raw.Result.Should().BeNull();

            CommandHandlerImpl.Called.Should().BeTrue();
        }
    }

    #endregion

    #region [CommandHandler] on IAutoService explicitly implemented.

    public class CommandHandlerExplicitImpl : ICommandHandler
    {
        public static bool Called;

        void ICommandHandler.Handle( IActivityMonitor m, ITestCommand c )
        {
            Called = true;
            m.Info( "Explicit Hop!" );
        }
    }

    [Test]
    public async Task calling_explicit_AutoService_implementation_Async()
    {
        await using var auto = await TestHelper.CreateAutomaticServicesWithMonitorAsync(
            [
                typeof( RawCrisExecutor ),
                typeof( ITestCommand ),
                typeof( CommandHandlerExplicitImpl )
            ] );
        using( var scope = auto.Services.CreateScope() )
        {
            var services = scope.ServiceProvider;
            var executor = services.GetRequiredService<RawCrisExecutor>();
            var cmd = services.GetRequiredService<IPocoFactory<ITestCommand>>().Create();

            CommandHandlerExplicitImpl.Called = false;

            var raw = await executor.RawExecuteAsync( services, cmd );
            raw.Result.Should().BeNull();

            CommandHandlerExplicitImpl.Called.Should().BeTrue();
        }
    }

    #endregion


    #region Unknown type injection

    public interface IFluentEmail
    {
    }

    public class SomeHandler : IAutoService
    {
        [CommandHandler]
        public void Handle( ITestCommand command, IFluentEmail someObject )
        {
        }
    }

    [Test]
    public async Task unknown_types_are_injected_Async()
    {
        await using var auto = await TestHelper.CreateAutomaticServicesWithMonitorAsync(
            [
                typeof( RawCrisExecutor ),
                typeof( ITestCommand ),
                typeof( SomeHandler )
            ] );

        using( var scope = auto.Services.CreateScope() )
        {
            var services = scope.ServiceProvider;
            var executor = services.GetRequiredService<RawCrisExecutor>();
            var cmd = services.GetRequiredService<IPocoFactory<ITestCommand>>().Create();

            var raw = await executor.RawExecuteAsync( services, cmd );
            var e = raw.Result as ICrisResultError;
            Throw.DebugAssert( e != null );
            e.Errors.Single().Text.Should().Be( "While executing 'CK.Cris.Executor.Tests.RawCrisExecutorCommandTests.ITestCommand'." );
        }

    }

    #endregion
}
