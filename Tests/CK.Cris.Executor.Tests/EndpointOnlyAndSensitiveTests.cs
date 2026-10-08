using CK.Core;
using CK.Cris.AmbientValues;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.Executor.Tests;

[TestFixture]
public class EndpointOnlyAndSensitiveTests
{
    public interface ISecretCommand : ICommand<string>
    {
        string Name { get; set; }

        [Sensitive]
        string Secret { get; set; }
    }

    public interface IPlainCommand : ICommand<string>
    {
        string Name { get; set; }
    }

    public interface ISourcedEvent : IEvent, IEventSourceCommandPart
    {
    }

    public sealed class Handlers : ISingletonAutoService
    {
        [CommandHandler]
        public async Task<string> HandleAsync( ICrisCommandContext ctx, ISecretCommand c )
        {
            await ctx.EmitEventAsync<ISourcedEvent>( e => { } );
            return c.Name;
        }

        [CommandHandler]
        public async Task<string> HandleAsync( ICrisCommandContext ctx, IPlainCommand c )
        {
            await ctx.EmitEventAsync<ISourcedEvent>( e => { } );
            return c.Name;
        }
    }

    [Test]
    public async Task sensitive_commands_are_flagged_and_cannot_be_captured_by_their_events_Async()
    {
        await using var auto = await TestHelper.CreateAutomaticServicesWithMonitorAsync(
            [
                typeof( CrisExecutionContext ),
                typeof( RawCrisExecutor ),
                typeof( DarkSideCrisEventHub ),
                typeof( ISecretCommand ),
                typeof( IPlainCommand ),
                typeof( ISourcedEvent ),
                typeof( Handlers ),
                typeof( ICrisResultError )
            ] );
        using var scope = auto.Services.CreateScope();
        var services = scope.ServiceProvider;
        var poco = services.GetRequiredService<PocoDirectory>();
        var executor = services.GetRequiredService<CrisExecutionContext>();

        var secret = poco.Create<ISecretCommand>( c => { c.Name = "S"; c.Secret = "do-not-log"; } );
        secret.CrisPocoModel.HasSensitiveData.ShouldBeTrue();
        var plain = poco.Create<IPlainCommand>( c => c.Name = "P" );
        plain.CrisPocoModel.HasSensitiveData.ShouldBeFalse();

        // A command with sensitive data cannot emit an event that captures it.
        var executedSecret = await executor.ExecuteRootCommandAsync( secret );
        executedSecret.Result.ShouldBeAssignableTo<ICrisResultError>();
        executedSecret.Events.ShouldBeEmpty();

        var executedPlain = await executor.ExecuteRootCommandAsync( plain );
        executedPlain.Events.ShouldHaveSingleItem().ShouldBeAssignableTo<ISourcedEvent>().SourceCommand.ShouldBeSameAs( plain );
    }

    public interface ISafePart : ICrisPocoPart
    {
        [AmbientServiceValue( isSafe: true )]
        int? Thing { get; set; }
    }

    public interface ISafeAmbientValues : IAmbientValues
    {
        int Thing { get; set; }
    }

    public interface ISafeCommand : ICommand, ISafePart
    {
    }

    public sealed class SafeHandler : IAutoService
    {
        [CommandHandler]
        public void Handle( ISafeCommand c ) { }
    }

    public sealed class SafeValidator : IAutoService
    {
        [IncomingValidator]
        public void Validate( UserMessageCollector c, ISafePart p ) { }
    }

    static CK.Setup.EngineConfiguration CreateSafeConfiguration( bool withValidator )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ),
                                              typeof( IAmbientValuesCollectCommand ),
                                              typeof( AmbientValuesService ),
                                              typeof( ISafePart ),
                                              typeof( ISafeAmbientValues ),
                                              typeof( ISafeCommand ),
                                              typeof( SafeHandler ) );
        if( withValidator ) configuration.FirstBinPath.Types.Add( typeof( SafeValidator ) );
        return configuration;
    }

    [TestCase( true )]
    [TestCase( false )]
    public async Task a_safe_ambient_value_checked_by_no_validator_is_warned_Async( bool withValidator )
    {
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            await using var auto = (await CreateSafeConfiguration( withValidator ).RunSuccessfullyAsync()).CreateAutomaticServices();
            bool warned = texts.Any( t => t.Contains( "Safe ambient value 'Thing'" ) && t.Contains( "is checked by no [IncomingValidator]" ) );
            warned.ShouldBe( !withValidator );
        }
    }

    public interface IEndpointCultureCommand : ICommandEndpointOnly, ICommandCurrentCulture
    {
    }

    public sealed class EndpointCultureHandler : IAutoService
    {
        [CommandHandler]
        public void Handle( IEndpointCultureCommand c ) { }
    }

    [Test]
    public async Task an_endpoint_only_command_cannot_have_ambient_services_configurators_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ),
                                              typeof( RawCrisReceiver ),
                                              typeof( CrisCultureService ),
                                              typeof( NormalizedCultureInfo ),
                                              typeof( IEndpointCultureCommand ),
                                              typeof( EndpointCultureHandler ) );
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            var r = await configuration.RunAsync();
            r.Status.ShouldBe( CK.Setup.RunStatus.Failed );
            texts.ShouldContain( t => t.Contains( "is an ICommandEndpointOnly: it cannot have [ConfigureAmbientServices] handlers" ) );
        }
    }

    public interface IRedirectCommand : ICommand<RedirectedUrl>
    {
        string Target { get; set; }
    }

    public sealed class RedirectHandler : IAutoService
    {
        [CommandHandler]
        public RedirectedUrl Handle( IRedirectCommand c ) => new RedirectedUrl( c.Target, true );
    }

    [Test]
    public async Task RedirectedUrl_is_a_valid_command_result_Async()
    {
        await using var auto = await TestHelper.CreateAutomaticServicesWithMonitorAsync(
            [
                typeof( RawCrisExecutor ),
                typeof( IRedirectCommand ),
                typeof( RedirectHandler )
            ] );
        using var scope = auto.Services.CreateScope();
        var services = scope.ServiceProvider;
        var cmd = services.GetRequiredService<PocoDirectory>().Create<IRedirectCommand>( c => c.Target = "https://idp.example.com/authorize" );
        cmd.CrisPocoModel.ResultType.ShouldBe( typeof( RedirectedUrl ) );
        var r = await services.GetRequiredService<RawCrisExecutor>().RawExecuteAsync( services, cmd );
        r.Result.ShouldBe( new RedirectedUrl( "https://idp.example.com/authorize", true ) );
    }
}
