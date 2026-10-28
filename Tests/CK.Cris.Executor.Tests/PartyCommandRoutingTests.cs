using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.Executor.Tests;

/// <summary>
/// <see cref="ICrisRemoteDispatcher.RoutePartyCommand(IServiceProvider, IAbstractCommand)"/>: a party command is routed
/// by its target before any local execution, even when it has a local handler.
/// </summary>
[TestFixture]
public class PartyCommandRoutingTests
{
    public interface IHandledPartyCommand : ICommand<int>, ICommandTargetParty
    {
    }

    public interface IAsyncHandledPartyCommand : ICommand<int>, ICommandTargetParty
    {
    }

    public interface IUnhandledPartyCommand : ICommand<int>, ICommandTargetParty
    {
    }

    public sealed class Handler : IAutoService
    {
        public static int Count;

        [CommandHandler]
        public int Handle( IHandledPartyCommand c ) { ++Count; return 1; }

        [CommandHandler]
        public async Task<int> HandleAsync( IAsyncHandledPartyCommand c )
        {
            await Task.Yield();
            ++Count;
            return 2;
        }
    }

    // "Here" is the current party: anything else is "remote" and answers 3712.
    public sealed class FakeRouter : ICrisRemoteDispatcher
    {
        public static int DispatchCount;

        public Task<RawCrisExecutor.RawResult> DispatchAsync( IServiceProvider services, IAbstractCommand command )
        {
            ++DispatchCount;
            return Task.FromResult( new RawCrisExecutor.RawResult( -1, null ) );
        }

        public Task<RawCrisExecutor.RawResult>? RoutePartyCommand( IServiceProvider services, IAbstractCommand command )
        {
            return ((ICommandTargetParty)command).TargetPartyFullName == "Here"
                    ? null
                    : Task.FromResult( new RawCrisExecutor.RawResult( 3712, null ) );
        }
    }

    [Test]
    public async Task a_party_command_is_routed_by_its_target_before_any_local_execution_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                              typeof( IHandledPartyCommand ),
                                              typeof( IAsyncHandledPartyCommand ),
                                              typeof( IUnhandledPartyCommand ),
                                              typeof( Handler ),
                                              typeof( FakeRouter ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        using var scope = auto.Services.CreateScope();
        var services = scope.ServiceProvider;
        var executor = services.GetRequiredService<RawCrisExecutor>();
        var poco = services.GetRequiredService<PocoDirectory>();
        Handler.Count = 0;
        FakeRouter.DispatchCount = 0;

        // Handled here: the target is the current party.
        (await executor.RawExecuteAsync( services, poco.Create<IHandledPartyCommand>( c => c.TargetPartyFullName = "Here" ) )).Result.ShouldBe( 1 );
        (await executor.RawExecuteAsync( services, poco.Create<IAsyncHandledPartyCommand>( c => c.TargetPartyFullName = "Here" ) )).Result.ShouldBe( 2 );
        Handler.Count.ShouldBe( 2 );

        // Routed although a local handler exists (sync and async generated code).
        (await executor.RawExecuteAsync( services, poco.Create<IHandledPartyCommand>( c => c.TargetPartyFullName = "Remote" ) )).Result.ShouldBe( 3712 );
        (await executor.RawExecuteAsync( services, poco.Create<IAsyncHandledPartyCommand>( c => c.TargetPartyFullName = "Remote" ) )).Result.ShouldBe( 3712 );
        Handler.Count.ShouldBe( 2 );

        // Without local handler: routed, or "no handler" when the target is here (never a singleton dispatch).
        (await executor.RawExecuteAsync( services, poco.Create<IUnhandledPartyCommand>( c => c.TargetPartyFullName = "Remote" ) )).Result.ShouldBe( 3712 );
        (await executor.RawExecuteAsync( services, poco.Create<IUnhandledPartyCommand>( c => c.TargetPartyFullName = "Here" ) )).Result
            .ShouldBeAssignableTo<ICrisResultError>().Errors.Single().ResName.ShouldBe( "Cris.MissingCommandHandler" );
        FakeRouter.DispatchCount.ShouldBe( 0 );
    }
}
