using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.BackgroundExecutor.Tests;

[TestFixture]
public class EndpointOnlyTests
{
    public interface IEndpointCommand : ICommandEndpointOnly
    {
    }

    public sealed class EndpointHandler : IAutoService
    {
        [CommandHandler]
        public void Handle( IEndpointCommand c ) { }
    }

    [Test]
    public async Task an_endpoint_only_command_cannot_be_executed_in_the_background_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IEndpointCommand ),
                                              typeof( EndpointHandler ),
                                              typeof( CrisBackgroundExecutorService ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();

        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var executor = auto.Services.GetRequiredService<CrisBackgroundExecutorService>();
        Util.Invokable( () => executor.Submit( TestHelper.Monitor, poco.Create<IEndpointCommand>(), null ) )
            .ShouldThrow<ArgumentException>()
            .Message.ShouldContain( "can only be executed by its receiving endpoint" );
    }

    [Test]
    public async Task an_endpoint_only_command_cannot_be_delayed_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IEndpointCommand ),
                                              typeof( EndpointHandler ),
                                              typeof( IDelayedCommand ),
                                              typeof( CrisDelayedCommandService ),
                                              typeof( CrisBackgroundExecutorService ),
                                              typeof( RawCrisReceiver ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();

        using var scope = auto.Services.CreateScope();
        var services = scope.ServiceProvider;
        var poco = services.GetRequiredService<PocoDirectory>();
        var delayed = poco.Create<IDelayedCommand>( d =>
        {
            d.ExecutionDate = DateTime.UtcNow.AddMinutes( 5 );
            d.Command = poco.Create<IEndpointCommand>();
        } );
        var v = await services.GetRequiredService<RawCrisReceiver>().IncomingValidateAsync( TestHelper.Monitor, services, delayed );
        v.Success.ShouldBeFalse();
        v.ValidationMessages.Where( m => m.Level == UserMessageLevel.Error ).Select( m => m.Text )
            .ShouldContain( t => t.Contains( "is executed only by its receiving endpoint: it cannot be delayed." ) );
    }
}
