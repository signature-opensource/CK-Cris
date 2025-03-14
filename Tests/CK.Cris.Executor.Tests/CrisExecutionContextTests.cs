using CK.Core;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.Executor.Tests;

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

    public interface ICallerOnlyFinalEvent : IEvent
    {
    }

    public interface IStupidCommand : ICommand<int>
    {
        public static int CallCount;

        string Message { get; set; }
    }

    public interface IFinalCommand : ICommand
    {
        public static int CallCount;
    }

    public class Handlers : ISingletonAutoService
    {
        [CommandHandler]
        public async Task<int> HandleStupidCommandAsync( IActivityMonitor monitor, ICrisCommandContext ctx, IStupidCommand e )
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
        public async Task OnIRoutedImmediateEventCallStupidCommandAsync( ICrisEventContext ctx, IRoutedImmediateEvent e )
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
        public async Task OnIRoutedEventAsync( ICrisEventContext ctx, IRoutedEvent e )
        {
            ++IRoutedEvent.CallCount;
            ctx.Monitor.Trace( $"IRoutedEvent called. Executing IFinalCommand." );
            await ctx.ExecuteCommandAsync<IFinalCommand>( c => { } );
        }
    }

    public class FinalHandler : ISingletonAutoService
    {
        [CommandHandler]
        public async Task HandleFinalCommandAsync( ICrisCommandContext ctx, IFinalCommand f )
        {
            ++IFinalCommand.CallCount;
            ctx.Monitor.Info( $"Final called." );
            await ctx.EmitEventAsync<ICallerOnlyFinalEvent>( e => { } );
        }
    }

    [SetUp]
    public void ResetCounters()
    {
        IRoutedEvent.CallCount = 0;
        IRoutedImmediateEvent.CallCount = 0;
        ICallerOnlyImmediateEvent.CallCount = 0;
        IStupidCommand.CallCount = 0;
        IFinalCommand.CallCount = 0;
    }

    [Test]
    public async Task command_and_events_Async()
    {
        await using var auto = await TestHelper.CreateAutomaticServicesWithMonitorAsync(
            [
                typeof( CrisExecutionContext ),
                typeof( IStupidCommand ),
                typeof( IRoutedImmediateEvent ),
                typeof( IRoutedEvent ),
                typeof( Handlers ),
                typeof( IFinalCommand ),
                typeof( ICallerOnlyFinalEvent ),
                typeof( FinalHandler )
            ] );
        using( var scope = auto.Services.CreateScope() )
        {
            var services = scope.ServiceProvider;

            var executor = services.GetRequiredService<CrisExecutionContext>();
            var command = services.GetRequiredService<PocoDirectory>().Create<IStupidCommand>( c => c.Message = "Run!" );
            var executed = await executor.ExecuteRootCommandAsync( command );

            executed.Result.ShouldBe( 1 );
            IStupidCommand.CallCount.ShouldBe( 5 );
            IFinalCommand.CallCount.ShouldBe( 4 );

            executed.Events.Count().ShouldBe( 4 + 4 );
            executed.Events.Take( 4 ).ShouldAll( e => e.ShouldBeAssignableTo<IRoutedEvent>() );
            executed.Events.Skip( 4 ).ShouldAll( e => e.ShouldBeAssignableTo<ICallerOnlyFinalEvent>() );
        }
    }

    [Test]
    public async Task CrisEventHub_relays_the_events_Async()
    {
        await using var auto = await TestHelper.CreateAutomaticServicesWithMonitorAsync(
        [
            typeof( CrisExecutionContext ),
            typeof( IStupidCommand ),
            typeof( IRoutedImmediateEvent ),
            typeof( IRoutedEvent ),
            typeof( Handlers ),
            typeof( IFinalCommand ),
            typeof( ICallerOnlyFinalEvent ),
            typeof( FinalHandler )
        ] );
        using( var scope = auto.Services.CreateScope() )
        {
            var services = scope.ServiceProvider;

            // No concurrency issue here. We can keep things naive.
            var immediateEventCollector = new List<IEvent>();
            var allEventCollector = new List<IEvent>();
            var hub = services.GetRequiredService<CrisEventHub>();
            hub.Immediate.Sync += ( monitor, e ) => immediateEventCollector.Add( e );
            hub.All.Sync += ( monitor, e ) => allEventCollector.Add( e );

            var executor = services.GetRequiredService<CrisExecutionContext>();
            var command = services.GetRequiredService<PocoDirectory>().Create<IStupidCommand>( c => c.Message = "Run!" );
            var executed = await executor.ExecuteRootCommandAsync( command );

            executed.Events.Count().ShouldBe( 4 + 4 );
            executed.Events.Take( 4 ).ShouldAll( e => e.ShouldBeAssignableTo<IRoutedEvent>() );
            executed.Events.Skip( 4 ).ShouldAll( e => e.ShouldBeAssignableTo<ICallerOnlyFinalEvent>() );

            immediateEventCollector.Count.ShouldBe( 4 );
            immediateEventCollector.ShouldAll( e => e.ShouldBeAssignableTo<IRoutedImmediateEvent>() );

            allEventCollector.Take( immediateEventCollector.Count ).ShouldBe( immediateEventCollector );
            allEventCollector.Skip( 4 ).ShouldAll( e => e.ShouldBeAssignableTo<IRoutedEvent>() );
        }
    }
}
