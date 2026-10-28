using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.Executor.Tests;

[TestFixture]
public class RemoteDispatcherTests
{
    public interface IUnhandledCommand : ICommand<int>
    {
    }

    public interface IUnhandledEndpointOnlyCommand : ICommand<int>, ICommandEndpointOnly
    {
    }

    public interface IHandledCommand : ICommand<int>
    {
    }

    [Private]
    public interface IUnhandledPrivateCommand : ICommand<int>
    {
    }

    [Internal]
    public interface IInternalCommand : ICommand<int>
    {
    }

    // The most restrictive declaration of the family wins.
    [Private]
    public interface IMorePrivateInternalCommand : IInternalCommand
    {
    }

    [Internal]
    public interface IInternalEvent : IEvent
    {
    }

    public sealed class Handler : IAutoService
    {
        [CommandHandler]
        public int Handle( IHandledCommand c ) => 1;
    }

    public sealed class FakeDispatcher : ICrisRemoteDispatcher
    {
        public static int Count;

        public Task<RawCrisExecutor.RawResult> DispatchAsync( IServiceProvider services, IAbstractCommand command )
        {
            ++Count;
            return Task.FromResult( new RawCrisExecutor.RawResult( 3712, null ) );
        }
    }

    static async Task<AutomaticServices> CreateAsync( bool withDispatcher )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                              typeof( IUnhandledCommand ),
                                              typeof( IUnhandledEndpointOnlyCommand ),
                                              typeof( IHandledCommand ),
                                              typeof( IUnhandledPrivateCommand ),
                                              typeof( Handler ) );
        if( withDispatcher ) configuration.FirstBinPath.Types.Add( typeof( FakeDispatcher ) );
        return (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
    }

    [Test]
    public async Task a_command_without_handler_is_dispatched_Async()
    {
        await using var auto = await CreateAsync( withDispatcher: true );
        using var scope = auto.Services.CreateScope();
        var services = scope.ServiceProvider;
        var executor = services.GetRequiredService<RawCrisExecutor>();
        var poco = services.GetRequiredService<PocoDirectory>();
        FakeDispatcher.Count = 0;

        (await executor.RawExecuteAsync( services, poco.Create<IUnhandledCommand>() )).Result.ShouldBe( 3712 );
        FakeDispatcher.Count.ShouldBe( 1 );

        // A local handler always wins.
        (await executor.RawExecuteAsync( services, poco.Create<IHandledCommand>() )).Result.ShouldBe( 1 );
        FakeDispatcher.Count.ShouldBe( 1 );

        // An endpoint-only command is never dispatched.
        var r = await executor.RawExecuteAsync( services, poco.Create<IUnhandledEndpointOnlyCommand>() );
        r.Result.ShouldBeAssignableTo<ICrisResultError>()
                .Errors.Single().ResName.ShouldBe( "Cris.MissingCommandHandler" );
        FakeDispatcher.Count.ShouldBe( 1 );
    }

    [Test]
    public async Task without_dispatcher_a_command_without_handler_is_an_error_Async()
    {
        await using var auto = await CreateAsync( withDispatcher: false );
        using var scope = auto.Services.CreateScope();
        var services = scope.ServiceProvider;
        var executor = services.GetRequiredService<RawCrisExecutor>();
        var poco = services.GetRequiredService<PocoDirectory>();

        var r = await executor.RawExecuteAsync( services, poco.Create<IUnhandledCommand>() );
        r.Result.ShouldBeAssignableTo<ICrisResultError>()
                .Errors.Single().ResName.ShouldBe( "Cris.MissingCommandHandler" );
    }

    [Test]
    public async Task a_Private_command_is_never_dispatched_Async()
    {
        await using var auto = await CreateAsync( withDispatcher: true );
        using var scope = auto.Services.CreateScope();
        var services = scope.ServiceProvider;
        var executor = services.GetRequiredService<RawCrisExecutor>();
        var poco = services.GetRequiredService<PocoDirectory>();
        FakeDispatcher.Count = 0;

        var cmd = poco.Create<IUnhandledPrivateCommand>();
        cmd.CrisPocoModel.Exposure.ShouldBe( CrisExposure.Private );
        var r = await executor.RawExecuteAsync( services, cmd );
        var error = r.Result.ShouldBeAssignableTo<ICrisResultError>().Errors.Single();
        error.ResName.ShouldBe( "Cris.MissingCommandHandler" );
        error.Text.ShouldContain( "[Private]" );
        FakeDispatcher.Count.ShouldBe( 0 );

        poco.Create<IUnhandledCommand>().CrisPocoModel.Exposure.ShouldBe( CrisExposure.Public );
    }

    [Test]
    public async Task the_exposure_is_the_most_restrictive_of_the_family_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ), typeof( IInternalCommand ) );
        await using( var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices() )
        {
            auto.Services.GetRequiredService<PocoDirectory>().Create<IInternalCommand>().CrisPocoModel.Exposure.ShouldBe( CrisExposure.Internal );
        }
        configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ), typeof( IInternalCommand ), typeof( IMorePrivateInternalCommand ) );
        await using( var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices() )
        {
            auto.Services.GetRequiredService<PocoDirectory>().Create<IInternalCommand>().CrisPocoModel.Exposure.ShouldBe( CrisExposure.Private );
        }
    }

    [Test]
    public async Task Internal_and_Private_apply_to_commands_only_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ), typeof( IInternalEvent ) );
        await configuration.GetFailedAutomaticServicesAsync( "is an event: [Internal] and [Private] apply to commands only." );
    }
}
