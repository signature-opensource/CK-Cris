using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.Executor.Tests
{
    [TestFixture]
    public class RawCrisExecutorEventTests
    {
        public interface ITestEvent : IEvent
        {
        }

        public class EventSyncHandler : IAutoService
        {
            public static bool Called;

            [RoutedEventHandler]
            public void HandleEvent( ITestEvent e )
            {
                Called = true;
            }
        }

        public class EventAsyncHandler : IAutoService
        {
            [RoutedEventHandler]
            public Task HandleEventAsync( ITestEvent e )
            {
                EventSyncHandler.Called = true;
                return Task.CompletedTask;
            }
        }

        public class EventValueTaskAsyncHandler : IAutoService
        {
            [RoutedEventHandler]
            public ValueTask HandleEventAsync( ITestEvent e )
            {
                EventSyncHandler.Called = true;
                return ValueTask.CompletedTask;
            }
        }

        [TestCase( "Sync" )]
        [TestCase( "RefAsync" )]
        [TestCase( "ValAsync" )]
        public async Task executing_an_event_Async( string kind )
        {
            var c = TestHelper.CreateStObjCollector( typeof( RawCrisExecutor ),
                                                     typeof( CrisDirectory ),
                                                     typeof( ITestEvent ),
                                                     kind switch
                                                     {
                                                         "RefAsync" => typeof( EventAsyncHandler ),
                                                         "ValAsync" => typeof( EventValueTaskAsyncHandler ),
                                                         "Sync" => typeof( EventSyncHandler ),
                                                         _ => throw new NotImplementedException()
                                                     } );
            using var appServices = TestHelper.CreateAutomaticServicesWithMonitor( c ).Services;
            using( var scope = appServices.CreateScope() )
            {
                var services = scope.ServiceProvider;
                var executor = services.GetRequiredService<RawCrisExecutor>();
                var cmd = services.GetRequiredService<IPocoFactory<ITestEvent>>().Create();

                EventSyncHandler.Called = false;
                var result = await executor.RawExecuteAsync( services, cmd );
                result.Should().BeNull();
                EventSyncHandler.Called.Should().BeTrue();
            }

        }
    }
}
