using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.Executor.Tests;

[TestFixture]
public class RawCrisExecutorEventTests
{
    [RoutedEvent]
    public interface ITestEvent : IEvent
    {
        public static int CallCount;
    }

    public class EventSyncHandler : IAutoService
    {
        [RoutedEventHandler]
        public void HandleEvent( ITestEvent e )
        {
            ++ITestEvent.CallCount;
        }
    }

    public class EventAsyncHandler : IAutoService
    {
        [RoutedEventHandler]
        public Task HandleEventAsync( ITestEvent e )
        {
            ++ITestEvent.CallCount;
            return Task.CompletedTask;
        }
    }

    public class EventValueTaskAsyncHandler : IAutoService
    {
        [RoutedEventHandler]
        public ValueTask HandleEventAsync( ITestEvent e )
        {
            ++ITestEvent.CallCount;
            return ValueTask.CompletedTask;
        }
    }

    [TestCase( "Sync" )]
    [TestCase( "RefAsync" )]
    [TestCase( "ValAsync" )]
    public async Task dispatching_an_event_Async( string kind )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                              typeof( CrisDirectory ),
                                              typeof( ITestEvent ),
                                              kind switch
                                              {
                                                  "RefAsync" => typeof( EventAsyncHandler ),
                                                  "ValAsync" => typeof( EventValueTaskAsyncHandler ),
                                                  "Sync" => typeof( EventSyncHandler ),
                                                  _ => throw new NotImplementedException()
                                              } );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        using( var scope = auto.Services.CreateScope() )
        {
            var services = scope.ServiceProvider;
            var executor = services.GetRequiredService<RawCrisExecutor>();
            var e = services.GetRequiredService<IPocoFactory<ITestEvent>>().Create();

            ITestEvent.CallCount = 0;
            await executor.DispatchEventAsync( services, e );
            ITestEvent.CallCount.ShouldBe( 1 );
        }

    }

    [Test]
    public async Task dispatching_an_event_to_3_handlers_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                              typeof( CrisDirectory ),
                                              typeof( ITestEvent ),
                                              typeof( EventAsyncHandler ),
                                              typeof( EventValueTaskAsyncHandler ),
                                              typeof( EventSyncHandler ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        using( var scope = auto.Services.CreateScope() )
        {
            var services = scope.ServiceProvider;
            var executor = services.GetRequiredService<RawCrisExecutor>();
            var e = services.GetRequiredService<IPocoFactory<ITestEvent>>().Create();

            ITestEvent.CallCount = 0;
            await executor.DispatchEventAsync( services, e );
            ITestEvent.CallCount.ShouldBe( 3 );
        }
    }
}
