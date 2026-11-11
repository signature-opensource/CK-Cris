using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.BackgroundExecutor.Tests;

[TestFixture]
public class DelayedCommandGuardTests
{
    public interface IPublicCommand : ICommand
    {
    }

    [Internal]
    public interface IInternalCommand : ICommand
    {
    }

    [Private]
    public interface IPrivateCommand : ICommand
    {
    }

    public interface IEndpointCommand : ICommandEndpointOnly
    {
    }

    public sealed class Handlers : ISingletonAutoService
    {
        [CommandHandler]
        public void Handle( IPublicCommand c ) { }

        [CommandHandler]
        public void Handle( IInternalCommand c ) { }

        [CommandHandler]
        public void Handle( IPrivateCommand c ) { }

        [CommandHandler]
        public void Handle( IEndpointCommand c ) { }
    }

    public sealed class ExecutedEventHandler : ISingletonAutoService
    {
        public static TaskCompletionSource<IDelayedCommandExecutedEvent> Executed = new();

        [LocalEventHandler]
        public void On( IDelayedCommandExecutedEvent e ) => Executed.TrySetResult( e );
    }

    // A store of 2 commands whose OnCommandExecuting always throws.
    public class GuardedDelayedCommandService : CrisDelayedCommandService
    {
        public GuardedDelayedCommandService( CrisBackgroundExecutorService backgroundExecutorService,
                                             PocoDirectory pocoDirectory,
                                             RawCrisExecutor rawCrisExecutor,
                                             IEnumerable<IDeferredAmbientValuesCapture> captures )
            : base( backgroundExecutorService, pocoDirectory, rawCrisExecutor, captures )
        {
        }

        protected override int MaxMemoryStoreCount => 2;

        protected override void OnCommandExecuting( DelayedCommandEntry entry )
        {
            throw new InvalidOperationException( "OnCommandExecuting failure." );
        }
    }

    static async Task<AutomaticServices> CreateAsync()
    {
        ExecutedEventHandler.Executed = new( TaskCreationOptions.RunContinuationsAsynchronously );
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IPublicCommand ),
                                              typeof( IInternalCommand ),
                                              typeof( IPrivateCommand ),
                                              typeof( IEndpointCommand ),
                                              typeof( Handlers ),
                                              typeof( ExecutedEventHandler ),
                                              typeof( IDelayedCommand ),
                                              typeof( IDelayedCommandExecutedEvent ),
                                              typeof( GuardedDelayedCommandService ),
                                              typeof( CrisBackgroundExecutorService ),
                                              typeof( RawCrisReceiver ) );
        return (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
    }

    static async Task<string[]> IncomingValidateAsync( AutomaticServices auto, IDelayedCommand delayed )
    {
        using var scope = auto.Services.CreateScope();
        var services = scope.ServiceProvider;
        var v = await services.GetRequiredService<RawCrisReceiver>().IncomingValidateAsync( TestHelper.Monitor, services, delayed );
        return v.ValidationMessages.Where( m => m.Level == UserMessageLevel.Error ).Select( m => m.Text ).ToArray();
    }

    [TestCase( typeof( IPublicCommand ), null )]
    [TestCase( typeof( IInternalCommand ), "is Internal: it cannot be delayed by a Public" )]
    [TestCase( typeof( IPrivateCommand ), "is Private: it cannot be delayed by a Public" )]
    public async Task the_inner_command_cannot_be_more_restricted_than_the_delayed_command_Async( Type innerType, string? error )
    {
        await using var auto = await CreateAsync();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var delayed = poco.Create<IDelayedCommand>( d =>
        {
            d.ExecutionDate = DateTime.UtcNow.AddMinutes( 5 );
            d.Command = (IAbstractCommand)poco.Find( innerType )!.Create();
        } );
        var errors = await IncomingValidateAsync( auto, delayed );
        if( error == null ) errors.ShouldBeEmpty();
        else errors.ShouldContain( t => t.Contains( error ) );
    }

    [Test]
    public async Task the_execution_date_is_bounded_Async()
    {
        await using var auto = await CreateAsync();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var delayed = poco.Create<IDelayedCommand>( d =>
        {
            d.ExecutionDate = DateTime.UtcNow.AddDays( 31 );
            d.Command = poco.Create<IPublicCommand>();
        } );
        (await IncomingValidateAsync( auto, delayed )).ShouldContain( t => t.Contains( "is too far in the future" ) );
    }

    [Test]
    public async Task the_store_is_bounded_Async()
    {
        await using var auto = await CreateAsync();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var service = auto.Services.GetRequiredService<CrisDelayedCommandService>();
        IDelayedCommand Create() => poco.Create<IDelayedCommand>( d =>
        {
            d.ExecutionDate = DateTime.UtcNow.AddDays( 1 );
            d.Command = poco.Create<IPublicCommand>();
        } );
        await service.StoreAsync( TestHelper.Monitor, Create() );
        await service.StoreAsync( TestHelper.Monitor, Create() );
        (await IncomingValidateAsync( auto, Create() )).ShouldContain( t => t.Contains( "The delayed command store is full (2 commands)" ) );
        await Util.Awaitable( () => service.StoreAsync( TestHelper.Monitor, Create() ).AsTask() )
                  .ShouldThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task an_endpoint_only_command_cannot_be_stored_Async()
    {
        await using var auto = await CreateAsync();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var delayed = poco.Create<IDelayedCommand>( d =>
        {
            d.ExecutionDate = DateTime.UtcNow;
            d.AllowPastExecutionDate = true;
            d.Command = poco.Create<IEndpointCommand>();
        } );
        await Util.Awaitable( () => auto.Services.GetRequiredService<CrisDelayedCommandService>().StoreAsync( TestHelper.Monitor, delayed ).AsTask() )
                  .ShouldThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task an_exception_in_OnCommandExecuting_does_not_escape_the_timer_Async()
    {
        await using var auto = await CreateAsync();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var delayed = poco.Create<IDelayedCommand>( d =>
        {
            d.ExecutionDate = DateTime.UtcNow;
            d.AllowPastExecutionDate = true;
            d.Command = poco.Create<IPublicCommand>();
        } );
        // Before the fix, the exception ended the test host.
        await auto.Services.GetRequiredService<CrisDelayedCommandService>().StoreAsync( TestHelper.Monitor, delayed );
        var e = await ExecutedEventHandler.Executed.Task.WaitAsync( TimeSpan.FromSeconds( 5 ) );
        e.SourceCommand.ShouldBeSameAs( delayed );
    }
}
