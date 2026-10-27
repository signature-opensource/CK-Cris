using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.BackgroundExecutor.Tests;

[TestFixture]
public class ContainerConfiguredContextTests
{
    public interface IContextCommand : ICommand<bool>
    {
    }

    public sealed class ContextHandler : IRealObject
    {
        [CommandHandler]
        public bool Handle( IContextCommand command, ICrisEventContext eventContext, ICrisCommandContext commandContext )
        {
            // Both are the same scoped execution context.
            return ReferenceEquals( eventContext, commandContext );
        }
    }

    // The background container definition only registers ICrisCommandContext and ICrisEventContext:
    // the handler must not require the CrisExecutionContext class.
    [Test]
    public async Task handlers_resolve_the_context_interfaces_not_the_class_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IContextCommand ),
                                              typeof( ContextHandler ),
                                              typeof( CrisBackgroundExecutorService ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();

        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var executor = auto.Services.GetRequiredService<CrisBackgroundExecutorService>();
        var executing = executor.Submit( TestHelper.Monitor, poco.Create<IContextCommand>(), null );
        var executed = await executing.ExecutedCommand;
        executed.Result.ShouldBe( true );
    }

    // A service that only an endpoint container could configure: the background container doesn't.
    [ScopedContainerConfiguredService]
    public interface IEndpointThing : IAutoService
    {
    }

    public interface IEndpointBoundCommand : ICommand
    {
    }

    public sealed class EndpointBoundHandler : IRealObject
    {
        [CommandHandler]
        public void Handle( IEndpointBoundCommand command, IEndpointThing thing ) { }
    }

    [Test]
    public async Task a_command_whose_handler_needs_an_endpoint_service_cannot_be_deferred_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IContextCommand ),
                                              typeof( ContextHandler ),
                                              typeof( IEndpointThing ),
                                              typeof( IEndpointBoundCommand ),
                                              typeof( EndpointBoundHandler ),
                                              typeof( CrisBackgroundExecutorService ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();

        var issues = auto.Services.GetRequiredService<CrisBackgroundExecutorService>()
                                  .GetDeferralIssues( auto.Services.GetRequiredService<CrisDirectory>() );
        issues.Count.ShouldBe( 1 );
        issues[0].ShouldContain( "IEndpointBoundCommand" );
        issues[0].ShouldContain( "IEndpointThing" );
    }
}
