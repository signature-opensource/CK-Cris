using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.Executor.Tests;

/// <summary>
/// A handler parameter with a default value is optional: it is resolved with GetService (null when the service is not
/// available) instead of GetRequiredService.
/// </summary>
[TestFixture]
public class OptionalParameterTests
{
    public interface IOptionalCommand : ICommand<string>
    {
    }

    // Not a service: it is never available.
    public interface INotAvailable
    {
    }

    public sealed class AvailableService : IScopedAutoService
    {
    }

    public sealed class Handler : IAutoService
    {
        public static string? ValidatorSaw;

        [CommandHandler]
        public string Handle( IOptionalCommand c, INotAvailable? missing = null, AvailableService? available = null )
        {
            return $"missing:{missing == null}, available:{available != null}";
        }

        [IncomingValidator]
        public void Validate( UserMessageCollector collector, IOptionalCommand c, INotAvailable? missing = null, AvailableService? available = null )
        {
            ValidatorSaw = $"missing:{missing == null}, available:{available != null}";
        }
    }

    [Test]
    public async Task an_optional_parameter_is_null_when_its_service_is_not_available_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                              typeof( RawCrisReceiver ),
                                              typeof( IOptionalCommand ),
                                              typeof( AvailableService ),
                                              typeof( Handler ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        using var scope = auto.Services.CreateScope();
        var services = scope.ServiceProvider;
        var command = services.GetRequiredService<PocoDirectory>().Create<IOptionalCommand>();

        Handler.ValidatorSaw = null;
        var validation = await services.GetRequiredService<RawCrisReceiver>().IncomingValidateAsync( TestHelper.Monitor, services, command );
        validation.Success.ShouldBeTrue();
        Handler.ValidatorSaw.ShouldBe( "missing:True, available:True" );

        (await services.GetRequiredService<RawCrisExecutor>().RawExecuteAsync( services, command )).Result
            .ShouldBe( "missing:True, available:True" );
    }
}
