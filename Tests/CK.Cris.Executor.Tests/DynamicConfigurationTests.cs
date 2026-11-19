using CK.Core;
using CK.Cris.DynamicConfiguration;
using CK.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.Executor.Tests;

[TestFixture]
public class DynamicConfigurationTests
{
    [ConfigurationSection( "Fake" )]
    public sealed class FakeHost : ISingletonAutoService
    {
        int _runnerCount = 1;

        [ConfigurationLevel( ConfigurationLevel.Run )]
        public int RunnerCount
        {
            get => _runnerCount;
            set
            {
                Throw.CheckOutOfRangeArgument( value >= 1 );
                _runnerCount = value;
            }
        }

        [ConfigurationLevel( ConfigurationLevel.Run )]
        public FakeOptions Options { get; } = new FakeOptions();
    }

    public sealed class FakeOptions
    {
        [ConfigurationLevel( ConfigurationLevel.Run )]
        public TimeSpan Delay { get; set; }

        [ConfigurationLevel( ConfigurationLevel.Run )]
        public DayOfWeek Day { get; set; }
    }

    public sealed class FakeStore : IDynamicConfigurationStore
    {
        public Dictionary<string, DynamicConfigurationRecord> Records { get; } = new( StringComparer.OrdinalIgnoreCase );

        public IReadOnlyDictionary<string, DynamicConfigurationRecord> Load( IActivityMonitor monitor ) => new Dictionary<string, DynamicConfigurationRecord>( Records );

        public void Save( IActivityMonitor monitor, IReadOnlyDictionary<string, DynamicConfigurationRecord> records )
        {
            Records.Clear();
            foreach( var (k, v) in records ) Records.Add( k, v );
        }
    }

    static IConfiguration Configuration( params (string Key, string Value)[] values )
    {
        var d = new Dictionary<string, string?>();
        foreach( var (k, v) in values ) d[k] = v;
        return new ConfigurationBuilder().AddInMemoryCollection( d ).Build();
    }

    [Test]
    public async Task the_run_properties_are_initialized_changed_recorded_and_restricted_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( FakeHost ), typeof( FakeStore ), typeof( DynamicConfigurationService ), typeof( RunPropertyDirectory ) );
        // What the App builder writes for a "$LOCK": { "Fake:Options:Delay": "Init" }.
        configuration.FirstBinPath.LockedRunValues.Add( "Fake:Options:Delay" );
        var config = Configuration( ("Fake:RunnerCount", "3"), ("Fake:Options:Delay", "00:00:02") );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices( configureServices: services =>
        {
            services.AddSingleton( config );
        } );
        var services = auto.Services;
        var host = services.GetRequiredService<FakeHost>();
        var store = services.GetRequiredService<FakeStore>();
        var directory = services.GetRequiredService<RunPropertyDirectory>();
        directory.Properties.Select( p => (p.Path, p.IsRun) ).ShouldBe( [("Fake:Options:Day", true), ("Fake:Options:Delay", false), ("Fake:RunnerCount", true)] );

        // Initialized from the configuration (the restricted one too).
        var service = services.GetRequiredService<DynamicConfigurationService>();
        service.Initialize( TestHelper.Monitor );
        host.RunnerCount.ShouldBe( 3 );
        host.Options.Delay.ShouldBe( TimeSpan.FromSeconds( 2 ) );

        // Changed (a number or a string), recorded with the configured value it overrides.
        service.Configure( TestHelper.Monitor, "fake:runnerCount", 5.0, "3", out var message ).ShouldBeTrue( message );
        host.RunnerCount.ShouldBe( 5 );
        store.Records["Fake:RunnerCount"].ShouldBe( new DynamicConfigurationRecord( "5", "3" ) );
        service.Configure( TestHelper.Monitor, "Fake:Options:Day", "friday", "Sunday", out message ).ShouldBeTrue( message );
        host.Options.Day.ShouldBe( DayOfWeek.Friday );

        // The optimistic concurrency, the validation of the setter, the restricted and the unknown properties.
        service.Configure( TestHelper.Monitor, "Fake:RunnerCount", 6, 3, out message ).ShouldBeFalse();
        message.ShouldContain( "is '5'" );
        service.Configure( TestHelper.Monitor, "Fake:RunnerCount", 0, 5, out message ).ShouldBeFalse();
        host.RunnerCount.ShouldBe( 5 );
        service.Configure( TestHelper.Monitor, "Fake:Options:Day", "Someday", "Friday", out message ).ShouldBeFalse();
        message.ShouldContain( "is not a value of DayOfWeek" );
        service.Configure( TestHelper.Monitor, "Fake:Options:Delay", "00:00:05", "00:00:02", out message ).ShouldBeFalse();
        message.ShouldContain( "the configuration restricts it" );
        service.Configure( TestHelper.Monitor, "Fake:Nope", 1, null, out message ).ShouldBeFalse();

        // At the next start, the recorded value wins over the unchanged configured value...
        var restarted = new DynamicConfigurationService( services, config, directory, store );
        host.RunnerCount = 1;
        restarted.Initialize( TestHelper.Monitor );
        host.RunnerCount.ShouldBe( 5 );
        // ...and a configured value changed since then wins (the latest decision): the record is dropped.
        var changed = new DynamicConfigurationService( services, Configuration( ("Fake:RunnerCount", "4") ), directory, store );
        changed.Initialize( TestHelper.Monitor );
        host.RunnerCount.ShouldBe( 4 );
        store.Records.ShouldNotContainKey( "Fake:RunnerCount" );
        host.Options.Day.ShouldBe( DayOfWeek.Friday );
    }
}
