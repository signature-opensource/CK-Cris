using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.BackgroundExecutor.Tests;

[TestFixture]
public class DelayedCommandTests
{
    public interface ISucceedingCommand : ICommand
    {
    }

    public interface IFailingRestoreCommand : ICommand
    {
    }

    public sealed class Handlers : ISingletonAutoService
    {
        [CommandHandler]
        public void Handle( ISucceedingCommand c ) { }

        [CommandHandler]
        public void Handle( IFailingRestoreCommand c ) { }

        [RestoreAmbientServices]
        public void Restore( IFailingRestoreCommand c, AmbientServiceHub hub )
        {
            throw new InvalidOperationException( "Restore failure." );
        }
    }

    public sealed class ExecutedEventHandler : ISingletonAutoService
    {
        public static TaskCompletionSource<IDelayedCommandExecutedEvent> Executed = new();

        [RoutedEventHandler]
        public void On( IDelayedCommandExecutedEvent e ) => Executed.TrySetResult( e );
    }

    public class TestDelayedCommandService : CrisDelayedCommandService
    {
        public static TaskCompletionSource<(DelayedCommandEntry Entry, IExecutedCommand Command)> RestoreFailed = new();

        public TestDelayedCommandService( CrisBackgroundExecutorService backgroundExecutorService,
                                          PocoDirectory pocoDirectory,
                                          RawCrisExecutor rawCrisExecutor )
            : base( backgroundExecutorService, pocoDirectory, rawCrisExecutor )
        {
        }

        protected override Task OnRestoreFailedAsync( IActivityMonitor monitor, DelayedCommandEntry entry, IExecutedCommand command )
        {
            RestoreFailed.TrySetResult( (entry, command) );
            return Task.CompletedTask;
        }
    }

    static async Task<AutomaticServices> CreateAsync()
    {
        ExecutedEventHandler.Executed = new( TaskCreationOptions.RunContinuationsAsynchronously );
        TestDelayedCommandService.RestoreFailed = new( TaskCreationOptions.RunContinuationsAsynchronously );
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ISucceedingCommand ),
                                              typeof( IFailingRestoreCommand ),
                                              typeof( Handlers ),
                                              typeof( ExecutedEventHandler ),
                                              typeof( IDelayedCommand ),
                                              typeof( IDelayedCommandExecutedEvent ),
                                              typeof( TestDelayedCommandService ),
                                              typeof( CrisBackgroundExecutorService ),
                                              typeof( RawCrisReceiver ) );
        return (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
    }

    [Test]
    public async Task the_executed_event_SourceCommand_is_the_delayed_command_Async()
    {
        await using var auto = await CreateAsync();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var delayed = poco.Create<IDelayedCommand>( d =>
        {
            d.ExecutionDate = DateTime.UtcNow;
            d.AllowPastExecutionDate = true;
            d.Command = poco.Create<ISucceedingCommand>();
        } );
        await auto.Services.GetRequiredService<CrisDelayedCommandService>().StoreAsync( TestHelper.Monitor, delayed );

        var e = await ExecutedEventHandler.Executed.Task.WaitAsync( TimeSpan.FromSeconds( 5 ) );
        e.SourceCommand.ShouldBeSameAs( delayed );
        e.Command.ShouldBeSameAs( delayed.Command );
        TestDelayedCommandService.RestoreFailed.Task.IsCompleted.ShouldBeFalse();
    }

    [Test]
    public async Task OnRestoreFailedAsync_is_called_and_no_event_is_raised_when_the_restore_fails_Async()
    {
        await using var auto = await CreateAsync();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var delayed = poco.Create<IDelayedCommand>( d =>
        {
            d.ExecutionDate = DateTime.UtcNow;
            d.AllowPastExecutionDate = true;
            d.Command = poco.Create<IFailingRestoreCommand>();
        } );
        await auto.Services.GetRequiredService<CrisDelayedCommandService>().StoreAsync( TestHelper.Monitor, delayed );

        var (entry, command) = await TestDelayedCommandService.RestoreFailed.Task.WaitAsync( TimeSpan.FromSeconds( 5 ) );
        entry.DelayedCommand.ShouldBeSameAs( delayed );
        command.Command.ShouldBeSameAs( delayed.Command );
        command.Result.ShouldBeAssignableTo<ICrisResultError>();
        ExecutedEventHandler.Executed.Task.IsCompleted.ShouldBeFalse();
    }
}
