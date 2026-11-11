using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.BackgroundExecutor.Tests;

[TestFixture]
public class FinalResultTests
{
    public interface IOkCommand : ICommand<int>
    {
    }

    public sealed class Handler : IAutoService
    {
        [CommandHandler]
        public int Handle( IOkCommand c ) => 42;
    }

    // Collects the texts of every monitor (the runners' monitors included).
    sealed class TextCollector : ActivityMonitorClient
    {
        public static readonly ConcurrentQueue<string> Texts = new();

        protected override void OnUnfilteredLog( ref ActivityMonitorLogData data ) => Texts.Enqueue( data.Text );

        protected override void OnOpenGroup( IActivityLogGroup group ) => Texts.Enqueue( group.Data.Text );
    }

    [Test]
    public async Task a_failing_onExecutedCommand_callback_does_not_set_a_second_final_result_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IOkCommand ), typeof( Handler ), typeof( CrisBackgroundExecutorService ) );
        var previous = ActivityMonitor.AutoConfiguration;
        ActivityMonitor.AutoConfiguration = previous + ( m => m.Output.RegisterClient( new TextCollector() ) );
        TextCollector.Texts.Clear();
        try
        {
            await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
            var poco = auto.Services.GetRequiredService<PocoDirectory>();
            var executor = auto.Services.GetRequiredService<CrisBackgroundExecutorService>();
            int callCount = 0;
            var callbackDone = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
            var executing = executor.Submit( TestHelper.Monitor, poco.Create<IOkCommand>(), null, onExecutedCommand: ( m, e, s ) =>
            {
                Interlocked.Increment( ref callCount );
                callbackDone.TrySetResult();
                throw new InvalidOperationException( "Callback failure." );
            } );
            (await executing.ExecutedCommand.WaitAsync( TimeSpan.FromSeconds( 5 ) )).Result.ShouldBe( 42 );
            await callbackDone.Task.WaitAsync( TimeSpan.FromSeconds( 5 ) );
            // Lets the runner handle the callback exception.
            await Task.Delay( 200 );
            callCount.ShouldBe( 1 );
            TextCollector.Texts.ShouldContain( t => t.Contains( "While setting the final result of" ) );
            TextCollector.Texts.ShouldNotContain( t => t.Contains( "Unhandled exception while executing Job." ) );
        }
        finally
        {
            ActivityMonitor.AutoConfiguration = previous;
        }
    }
}
