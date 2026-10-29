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
    [LocalEvent]
    public interface ILocalEvent : IEvent
    {
        public static int CallCount;
    }

    [LocalEvent, ImmediateEvent]
    public interface ILocalImmediateEvent : IEvent
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

    public interface IStupidCommand : ICommand<IStupidCommandResult>
    {
        public static int CallCount;

        string Message { get; set; }
    }

    public interface IStupidCommandResult : ICrisBasicCommandResult
    {
        public int Value { get; set; }
    }

    public interface IFinalCommand : ICommand
    {
        public static int CallCount;
    }

    public class Handlers : ISingletonAutoService
    {
        [CommandHandler]
        public async Task<IStupidCommandResult> HandleStupidCommandAsync( IActivityMonitor monitor, ICrisCommandContext ctx, IStupidCommand e )
        {
            using( monitor.OpenInfo( $"Stupid command called. Message = {e.Message}" ) )
            {
                var c = ++IStupidCommand.CallCount;
                if( c < 5 )
                {
                    await ctx.EmitEventAsync<ILocalImmediateEvent>( e => { } );
                    await ctx.EmitEventAsync<ILocalEvent>( e => { } );
                }
                return e.CreateResult( r => r.Value = c );
            }
        }

        [LocalEventHandler]
        public async Task OnILocalImmediateEventCallStupidCommandAsync( ICrisEventContext ctx, ILocalImmediateEvent e )
        {
            ++ILocalImmediateEvent.CallCount;
            using( ctx.Monitor.OpenTrace( $"ILocalImmediateEvent => StupidCommand" ) )
            {
                var r = (IStupidCommandResult)(await ctx.ExecuteCommandAsync<IStupidCommand>( c => c.Message = "Triggered by an event." ))!;
                ctx.Monitor.CloseGroup( $"StupidCommand result = {r.Value}" );
            }
        }

        [LocalEventHandler]
        public void OnILocalImmediateEventTraceCallCounts( IActivityMonitor monitor, ILocalImmediateEvent e )
        {
            monitor.Trace( $"Calls count: IStupidCommand: {IStupidCommand.CallCount}, ILocalImmediateEvent: {ILocalImmediateEvent.CallCount}, ILocalEvent: {ILocalEvent.CallCount}." );
        }

        [LocalEventHandler]
        public async Task OnILocalEventAsync( ICrisEventContext ctx, ILocalEvent e )
        {
            ++ILocalEvent.CallCount;
            ctx.Monitor.Trace( $"ILocalEvent called. Executing IFinalCommand." );
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
        ILocalEvent.CallCount = 0;
        ILocalImmediateEvent.CallCount = 0;
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
                typeof( RawCrisExecutor ),
                typeof( DarkSideCrisEventHub ),
                typeof( IStupidCommand ), typeof( IStupidCommandResult ),
                typeof( ILocalImmediateEvent ),
                typeof( ILocalEvent ),
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

            executed.Result.ShouldNotBeNull()
                           .ShouldBeAssignableTo<IStupidCommandResult>().Value.ShouldBe( 1 );
            IStupidCommand.CallCount.ShouldBe( 5 );
            IFinalCommand.CallCount.ShouldBe( 4 );

            executed.Events.Count().ShouldBe( 4 + 4 );
            executed.Events.Take( 4 ).ShouldAll( e => e.ShouldBeAssignableTo<ILocalEvent>() );
            executed.Events.Skip( 4 ).ShouldAll( e => e.ShouldBeAssignableTo<ICallerOnlyFinalEvent>() );
        }
    }

    [Test]
    public async Task CrisEventHub_relays_the_events_Async()
    {
        await using var auto = await TestHelper.CreateAutomaticServicesWithMonitorAsync(
        [
            typeof( CrisExecutionContext ),
            typeof( RawCrisExecutor ),
            typeof( DarkSideCrisEventHub ),
            typeof( IStupidCommand ), typeof( IStupidCommandResult ),
            typeof( ILocalImmediateEvent ),
            typeof( ILocalEvent ),
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
            executed.Events.Take( 4 ).ShouldAll( e => e.ShouldBeAssignableTo<ILocalEvent>() );
            executed.Events.Skip( 4 ).ShouldAll( e => e.ShouldBeAssignableTo<ICallerOnlyFinalEvent>() );

            immediateEventCollector.Count.ShouldBe( 4 );
            immediateEventCollector.ShouldAll( e => e.ShouldBeAssignableTo<ILocalImmediateEvent>() );

            allEventCollector.Take( immediateEventCollector.Count ).ShouldBe( immediateEventCollector );
            allEventCollector.Skip( 4 ).ShouldAll( e => e.ShouldBeAssignableTo<ILocalEvent>() );
        }
    }

    public interface IStupidCommandMore : IStupidCommand, ICommand<IStupidCommandResultMore>
    {
    }

    public interface IStupidCommandResultMore : IStupidCommandResult
    {
    }

    public class StupidCommandMoreHandler : ISingletonAutoService
    {
        [CommandHandler]
        public IStupidCommandResultMore Handle( IStupidCommandMore c )
        {
            return c.CreateResult<IStupidCommandResultMore>( r => r.Value = 3712 );
        }
    }

    [Test]
    public async Task ExecuteRootCommand_Async()
    {
        await using var auto = await TestHelper.CreateAutomaticServicesWithMonitorAsync(
            [
                typeof( CrisExecutionContext ),
                typeof( RawCrisExecutor ),
                typeof( DarkSideCrisEventHub ),
                typeof( IStupidCommand ),
                typeof( IStupidCommandMore ), typeof( IStupidCommandResultMore ),
                typeof( StupidCommandMoreHandler )
            ] );
        using( var scope = auto.Services.CreateScope() )
        {
            var services = scope.ServiceProvider;

            var executor = services.GetRequiredService<CrisExecutionContext>();
            var command = services.GetRequiredService<PocoDirectory>().Create<IStupidCommandMore>( c => c.Message = "Run!" );
            var executed = await executor.ExecuteRootCommandAsync( command );

            executed.Result.ShouldNotBeNull()
                           .ShouldBeAssignableTo<IStupidCommandResultMore>().Value.ShouldBe( 3712 );
        }
    }


}
