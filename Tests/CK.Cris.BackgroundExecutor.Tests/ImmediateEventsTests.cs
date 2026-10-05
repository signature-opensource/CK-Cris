using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.BackgroundExecutor.Tests;

[TestFixture]
public class ImmediateEventsTests
{
    public interface IDummyCommand : ICommand
    {
    }

    [ImmediateEvent]
    public interface INumberedImmediateEvent : IEvent
    {
        int Number { get; set; }
    }

    [Test]
    public async Task ImmediateEvents_enumerates_all_the_events_in_order_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IDummyCommand ),
                                              typeof( INumberedImmediateEvent ),
                                              typeof( CrisBackgroundExecutorService ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();

        var executing = new ExecutingCommand<IDummyCommand>( poco.Create<IDummyCommand>(), TestHelper.Monitor.CreateToken( "Test" ) );
        var events = executing.ImmediateEvents;
        events.ShouldBeEmpty();

        int raisedCount = 0;
        events.Added.Sync += ( monitor, e ) => ++raisedCount;

        // An enumerator obtained before any event is live: it sees the events added later.
        using var earlyEnumerator = events.GetEnumerator();

        for( int i = 0; i < 5; i++ )
        {
            await executing.DarkSide.AddImmediateEventAsync( TestHelper.Monitor, poco.Create<INumberedImmediateEvent>( e => e.Number = i ) );
        }
        raisedCount.ShouldBe( 5 );
        events.Count.ShouldBe( 5 );
        events.Cast<INumberedImmediateEvent>().Select( e => e.Number ).ShouldBe( [0, 1, 2, 3, 4] );

        int seen = 0;
        while( earlyEnumerator.MoveNext() ) earlyEnumerator.Current.ShouldBeAssignableTo<INumberedImmediateEvent>()!.Number.ShouldBe( seen++ );
        seen.ShouldBe( 5 );
        // Once ended, an enumerator stays ended.
        earlyEnumerator.MoveNext().ShouldBeFalse();
    }
}
