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
/// <see cref="CrisExecutionContext.RawExecuteInlineAsync(AmbientServiceHub, IAbstractCommand)"/>: a command executed
/// with other ambient services, inline (no job, no runner).
/// </summary>
[TestFixture]
public class InlineExecutionTests
{
    // Executes IInlineCommand inline with the culture of its CultureName.
    public interface IOuterCommand : ICommand<string>
    {
        string CultureName { get; set; }
    }

    public interface IInlineCommand : ICommand<string>
    {
    }

    public interface INestedCommand : ICommand<string>
    {
    }

    // A caller-only event: returned to the caller.
    public interface IInlineDoneEvent : IEvent
    {
        string CultureName { get; set; }
    }

    // A routed event: dispatched where the command executed, in the inline scope.
    [RoutedEvent]
    public interface IInlineRoutedEvent : IEvent
    {
    }

    // Its culture is configured by the incoming validation (CrisCultureService [ConfigureAmbientServices]).
    public interface ICultureCommand : ICommand<string>, ICurrentCulturePart
    {
    }

    public sealed class Handlers : ISingletonAutoService
    {
        [CommandHandler]
        public string Handle( ICultureCommand c, ExtendedCultureInfo culture ) => culture.Name;

        [CommandHandler]
        public async Task<string> HandleAsync( IOuterCommand c, ICrisCommandContext context, AmbientServiceHub hub, ExtendedCultureInfo culture, PocoDirectory pocos )
        {
            var other = hub.CleanClone();
            other.Override<ExtendedCultureInfo>( NormalizedCultureInfo.EnsureNormalizedCultureInfo( c.CultureName ) );
            var r = await ((CrisExecutionContext)context).RawExecuteInlineAsync( other, pocos.Create<IInlineCommand>() );
            // The inline execution is independent: its routed event is already dispatched, in its scope.
            Handlers.RoutedEventCulture.ShouldBe( "fr" );
            // The outer scope is unchanged.
            return $"outer:{culture.Name} / {r.Result}";
        }

        [CommandHandler]
        public async Task<string> HandleAsync( IInlineCommand c, ICrisCommandContext context, ExtendedCultureInfo culture )
        {
            await context.EmitEventAsync<IInlineDoneEvent>( e => e.CultureName = culture.Name );
            await context.EmitEventAsync<IInlineRoutedEvent>( e => { } );
            // Dispatched when the inline command completes, not now.
            Handlers.RoutedEventCulture.ShouldBeNull();
            // A command executed by the inline handler runs in the inline scope.
            var nested = (string?)await context.ExecuteCommandAsync<INestedCommand>( _ => { } );
            return $"inline:{culture.Name}, {nested}";
        }

        [CommandHandler]
        public string Handle( INestedCommand c, ExtendedCultureInfo culture ) => $"nested:{culture.Name}";

        public static string? RoutedEventCulture;

        [RoutedEventHandler]
        public void On( IInlineRoutedEvent e, ExtendedCultureInfo culture ) => RoutedEventCulture = culture.Name;
    }

    [Test]
    public async Task a_job_executes_a_command_inline_with_other_ambient_services_on_a_single_runner_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IOuterCommand ),
                                              typeof( IInlineCommand ),
                                              typeof( INestedCommand ),
                                              typeof( IInlineDoneEvent ),
                                              typeof( IInlineRoutedEvent ),
                                              typeof( Handlers ),
                                              typeof( CrisBackgroundExecutorService ),
                                              typeof( RawCrisReceiver ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        Handlers.RoutedEventCulture = null;
        var host = auto.Services.GetRequiredService<CrisExecutionHost>();
        host.ParallelRunnerCount.ShouldBe( 1 );

        var pocos = auto.Services.GetRequiredService<PocoDirectory>();
        var background = auto.Services.GetRequiredService<CrisBackgroundExecutorService>();
        using var scope = auto.Services.CreateScope();
        var hub = scope.ServiceProvider.GetRequiredService<AmbientServiceHub>().CleanClone();
        hub.Override<ExtendedCultureInfo>( NormalizedCultureInfo.EnsureNormalizedCultureInfo( "en" ) );

        var executing = background.Submit( TestHelper.Monitor, pocos.Create<IOuterCommand>( c => c.CultureName = "fr" ), hub );
        var executed = await executing.ExecutedCommand.WaitAsync( TimeSpan.FromSeconds( 5 ) );

        executed.Result.ShouldBe( "outer:en / inline:fr, nested:fr" );
        // The caller-only event of the inline command is returned to the caller: it is one of the outer command.
        executed.Events.OfType<IInlineDoneEvent>().ShouldHaveSingleItem().CultureName.ShouldBe( "fr" );
        // Its routed event is not: it has been dispatched where the inline command executed.
        executed.Events.OfType<IInlineRoutedEvent>().ShouldBeEmpty();
    }

    [Test]
    public async Task the_ambient_services_configured_by_the_incoming_validation_of_a_job_are_honored_Async()
    {
        NormalizedCultureInfo.EnsureNormalizedCultureInfo( "fr" );
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ICultureCommand ),
                                              typeof( Handlers ),
                                              typeof( IOuterCommand ),
                                              typeof( IInlineCommand ),
                                              typeof( INestedCommand ),
                                              typeof( IInlineDoneEvent ),
                                              typeof( IInlineRoutedEvent ),
                                              typeof( CrisCultureService ),
                                              typeof( CrisBackgroundExecutorService ),
                                              typeof( RawCrisReceiver ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var pocos = auto.Services.GetRequiredService<PocoDirectory>();
        var background = auto.Services.GetRequiredService<CrisBackgroundExecutorService>();
        using var scope = auto.Services.CreateScope();
        var hub = scope.ServiceProvider.GetRequiredService<AmbientServiceHub>().CleanClone();
        hub.Override<ExtendedCultureInfo>( NormalizedCultureInfo.EnsureNormalizedCultureInfo( "en" ) );

        // As a command received by a channel: validated as an incoming command by the job.
        var executing = background.Submit( TestHelper.Monitor, pocos.Create<ICultureCommand>( c => c.CurrentCultureName = "fr" ), hub, incomingValidationCheck: true );
        (await executing.ExecutedCommand.WaitAsync( TimeSpan.FromSeconds( 5 ) )).Result.ShouldBe( "fr" );
    }

    [Test]
    public async Task an_endpoint_executes_a_root_command_inline_with_other_ambient_services_Async()
    {
        NormalizedCultureInfo.EnsureNormalizedCultureInfo( "fr" );
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ICultureCommand ),
                                              typeof( Handlers ),
                                              typeof( IOuterCommand ),
                                              typeof( IInlineCommand ),
                                              typeof( INestedCommand ),
                                              typeof( IInlineDoneEvent ),
                                              typeof( IInlineRoutedEvent ),
                                              typeof( CrisBackgroundExecutorService ),
                                              typeof( RawCrisReceiver ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        // Like an HTTP endpoint: a scope with its own execution context, not a job.
        using var scope = auto.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = new CrisExecutionContext( TestHelper.Monitor, services, services.GetRequiredService<DarkSideCrisEventHub>(), services.GetRequiredService<RawCrisExecutor>() );
        var hub = services.GetRequiredService<AmbientServiceHub>().CleanClone();
        hub.Override<ExtendedCultureInfo>( NormalizedCultureInfo.EnsureNormalizedCultureInfo( "fr" ) );

        var executed = await context.ExecuteRootCommandInlineAsync( hub, services.GetRequiredService<PocoDirectory>().Create<ICultureCommand>() );
        executed.Result.ShouldBe( "fr" );
        context.IsExecutingCommand.ShouldBeFalse();
    }
}
