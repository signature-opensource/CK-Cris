using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.Executor.Tests
{
    [TestFixture]
    public class CrisExecutionContextTests
    {
        [RoutedEvent]
        public interface IRoutedEvent : IEvent
        {
            public static int CallCount;
        }

        [RoutedEvent, ImmediateEvent]
        public interface IRoutedImmediateEvent : IEvent
        {
            public static int CallCount;
        }

        [ImmediateEvent]
        public interface ICallerOnlyImmediateEvent : IEvent
        {
            public static int CallCount;
        }

        public interface ICallerOnlyEvent : IEvent
        {
            public static int CallCount;
        }

        public interface IStupidCommand : ICommand<int>
        {
            public static int CallCount;

            string Message { get; set; }
        }


        public class Handlers : ISingletonAutoService
        {
            [CommandHandler]
            public async Task<int> HandleStupidCommandAsync( IActivityMonitor monitor, ICrisExecutionContext ctx, IStupidCommand e )
            {
                using( monitor.OpenInfo( $"Stupid command called. Message = {e.Message}" ) )
                {
                    var c = ++IStupidCommand.CallCount;
                    if( c < 5 )
                    {
                        await ctx.EmitEventAsync<IRoutedImmediateEvent>( e => { } );
                        await ctx.EmitEventAsync<IRoutedEvent>( e => { } );
                    }
                    return c;
                }
            }

            [RoutedEventHandler]
            public async Task OnIRoutedImmediateEventCallStupidCommandAsync( ICrisCallContext ctx, IRoutedImmediateEvent e )
            {
                ++IRoutedImmediateEvent.CallCount;
                using( ctx.Monitor.OpenTrace( $"IRoutedImmediateEvent => StupidCommand" ) )
                {
                    var r = (int)(await ctx.ExecuteCommandAsync<IStupidCommand>( c => c.Message = "Triggered by an event." ))!;
                    ctx.Monitor.CloseGroup( $"StupidCommand result = {r}" );
                }
            }

            [RoutedEventHandler]
            public void OnIRoutedImmediateEventTraceCallCounts( IActivityMonitor monitor, IRoutedImmediateEvent e )
            {
                monitor.Trace( $"Calls count: IStupidCommand: {IStupidCommand.CallCount}, IRoutedImmediateEvent: {IRoutedImmediateEvent.CallCount}, IRoutedEvent: {IRoutedEvent.CallCount}." );
            }

            [RoutedEventHandler]
            public void OnIRoutedEvent( IActivityMonitor monitor, IRoutedEvent e )
            {
                ++IRoutedEvent.CallCount;
                monitor.Trace( $"IRoutedEvent called" );
            }

        }

        [Test]
        public async Task command_and_events_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisExecutionContext ),
                                                     typeof( IStupidCommand ),
                                                     typeof( IRoutedImmediateEvent ),
                                                     typeof( IRoutedEvent ),
                                                     typeof( Handlers ) );
            using var appServices = TestHelper.CreateAutomaticServicesWithMonitor( c ).Services;
            using( var scope = appServices.CreateScope() )
            {
                var services = scope.ServiceProvider;

                var executor = services.GetRequiredService<CrisExecutionContext>();
                var command = services.GetRequiredService<PocoDirectory>().Create<IStupidCommand>( c => c.Message = "Run!" );
                var (result, events) = await executor.ExecuteAsync( command );

                result.Should().Be( 1 );
                IStupidCommand.CallCount.Should().Be( 5 );
                events.Should().HaveCount( 4 );
            }
        }

    }
}
