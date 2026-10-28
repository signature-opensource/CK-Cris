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

/// <summary>
/// <see cref="ContainerCommandExecutor.ConfigureReceptionAsync"/>: a receiving executor configures the ambient services
/// of a job before its incoming validation.
/// </summary>
[TestFixture]
public class ReceptionTests
{
    public interface IReceivedCommand : ICommand<string>
    {
    }

    public interface IRefusedAtReceptionCommand : ICommand<string>
    {
    }

    public sealed class Handlers : ISingletonAutoService
    {
        public static string? ValidatorCulture;

        [IncomingValidator]
        public void Validate( UserMessageCollector c, IReceivedCommand command, ExtendedCultureInfo culture ) => ValidatorCulture = culture.Name;

        [CommandHandler]
        public string Handle( IReceivedCommand c, ExtendedCultureInfo culture ) => culture.Name;

        [CommandHandler]
        public string Handle( IRefusedAtReceptionCommand c ) => "executed";
    }

    // Stands for a receiving executor that derives ambient services from its reception context (the identity of a
    // channel caller): here, a culture.
    public class ReceivingExecutor : CrisBackgroundExecutorService
    {
        public ReceivingExecutor( CrisExecutionHost executionHost, IDIContainer<CrisBackgroundDIContainerDefinition.Data> container )
            : base( executionHost, container )
        {
        }

        protected override ValueTask<(ICrisResultError? Error, AmbientServiceHub? Hub)> ConfigureReceptionAsync( IActivityMonitor monitor, CrisJob job, IServiceProvider services )
        {
            if( job.Command is IRefusedAtReceptionCommand )
            {
                var e = services.GetRequiredService<PocoDirectory>().Create<ICrisResultError>( e => e.Errors.Add( UserMessage.Error( NormalizedCultureInfo.CodeDefault, "Refused at reception.", "Test.Refused" ) ) );
                return ValueTask.FromResult<(ICrisResultError?, AmbientServiceHub?)>( (e, null) );
            }
            var hub = services.GetRequiredService<AmbientServiceHub>().CleanClone();
            hub.Override<ExtendedCultureInfo>( NormalizedCultureInfo.EnsureNormalizedCultureInfo( "fr" ) );
            return ValueTask.FromResult<(ICrisResultError?, AmbientServiceHub?)>( (null, hub) );
        }
    }

    [Test]
    public async Task the_reception_ambient_services_apply_to_the_incoming_validation_and_the_execution_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IReceivedCommand ),
                                              typeof( IRefusedAtReceptionCommand ),
                                              typeof( Handlers ),
                                              typeof( CrisBackgroundExecutorService ),
                                              typeof( ReceivingExecutor ),
                                              typeof( RawCrisReceiver ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var pocos = auto.Services.GetRequiredService<PocoDirectory>();
        var executor = auto.Services.GetRequiredService<CrisBackgroundExecutorService>();
        executor.ShouldBeAssignableTo<ReceivingExecutor>();
        using var scope = auto.Services.CreateScope();
        var hub = scope.ServiceProvider.GetRequiredService<AmbientServiceHub>().CleanClone();
        hub.Override<ExtendedCultureInfo>( NormalizedCultureInfo.EnsureNormalizedCultureInfo( "en" ) );

        Handlers.ValidatorCulture = null;
        var received = await executor.Submit( TestHelper.Monitor, pocos.Create<IReceivedCommand>(), hub, incomingValidationCheck: true )
                                     .ExecutedCommand.WaitAsync( TimeSpan.FromSeconds( 5 ) );
        received.Result.ShouldBe( "fr" );
        Handlers.ValidatorCulture.ShouldBe( "fr" );

        var refused = await executor.Submit( TestHelper.Monitor, pocos.Create<IRefusedAtReceptionCommand>(), hub, incomingValidationCheck: true )
                                    .ExecutedCommand.WaitAsync( TimeSpan.FromSeconds( 5 ) );
        refused.Result.ShouldBeAssignableTo<ICrisResultError>()!.Errors.Single().ResName.ShouldBe( "Test.Refused" );
    }
}
