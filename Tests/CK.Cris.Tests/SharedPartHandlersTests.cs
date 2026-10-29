using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.Tests;

[TestFixture]
public class SharedPartHandlersTests
{
    public interface ISharedPart : ICrisPocoPart
    {
        string? Data { get; set; }
    }

    public interface ISharedCommand : ICommand, ISharedPart
    {
    }

    [LocalEvent]
    public interface ISharedLocalEvent : IEvent, ISharedPart
    {
    }

    public interface ISharedCallerOnlyEvent : IEvent, ISharedPart
    {
    }

    public sealed class SharedPartService : ISingletonAutoService
    {
        [IncomingValidator]
        public void Validate( UserMessageCollector c, ISharedPart p ) { }

        [ConfigureAmbientServices]
        public void Configure( ISharedPart p, AmbientServiceHub hub ) { }

        [RestoreAmbientServices]
        public void Restore( ISharedPart p, AmbientServiceHub hub ) { }

        [CommandHandler]
        public void Handle( ISharedCommand c ) { }

        [LocalEventHandler]
        public void On( ISharedLocalEvent e ) { }
    }

    /// <summary>
    /// The [IncomingValidator], [ConfigureAmbientServices] and [RestoreAmbientServices] methods of a part
    /// target every command and event that carries the part. On the events, the ones that don't apply
    /// are forgotten: this must not depend on how the engine has been built (Debug or Release).
    /// </summary>
    [Test]
    public async Task part_handlers_are_forgotten_on_the_events_they_cannot_apply_to_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ),
                                              typeof( ISharedPart ),
                                              typeof( ISharedCommand ),
                                              typeof( ISharedLocalEvent ),
                                              typeof( ISharedCallerOnlyEvent ),
                                              typeof( SharedPartService ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<CrisDirectory>();
        ICrisPocoModel Model<T>() => d.CrisPocoModels.Single( m => typeof( T ).IsAssignableFrom( m.CommandType ) );

        // The Handlers list doesn't contain the configurators and the restorers.
        var command = Model<ISharedCommand>();
        command.Handlers.Select( h => h.Kind ).ShouldBe( [CrisHandlerKind.IncomingValidator, CrisHandlerKind.CommandHandler], ignoreOrder: true );
        command.EndpointMustConfigureServices.ShouldBeTrue();
        command.BackgroundMustRestoreServices.ShouldBeTrue();

        var local = Model<ISharedLocalEvent>();
        local.Handlers.Select( h => h.Kind ).ShouldBe( [CrisHandlerKind.LocalEventHandler] );
        local.EndpointMustConfigureServices.ShouldBeTrue();
        local.BackgroundMustRestoreServices.ShouldBeTrue();

        var callerOnly = Model<ISharedCallerOnlyEvent>();
        callerOnly.Handlers.ShouldBeEmpty();
        callerOnly.EndpointMustConfigureServices.ShouldBeFalse();
        callerOnly.BackgroundMustRestoreServices.ShouldBeFalse();
    }
}
