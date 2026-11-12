using CK.Core;
using CK.Poco.Exc.Json;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.Executor.Tests;

[TestFixture]
public class SensitiveDataTests
{
    public interface ISecretCommand : ICommand
    {
        [Sensitive]
        string Secret { get; set; }

        string Public { get; set; }
    }

    // Like an internal operation that carries a login command: the sensitive data is nested, behind an abstract property.
    public interface IWrapperCommand : ICommand
    {
        IAbstractCommand? Inner { get; set; }
    }

    public interface IPlainCommand : ICommand
    {
        string Name { get; set; }
    }

    public interface IBadSensitiveCommand : ICommand
    {
        [Sensitive]
        int NotAString { get; set; }
    }

    static async Task<AutomaticServices> CreateAsync()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( ISecretCommand ), typeof( IWrapperCommand ), typeof( IPlainCommand ) );
        return (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
    }

    [Test]
    public async Task HasSensitiveData_covers_the_nested_Cris_Pocos_Async()
    {
        await using var auto = await CreateAsync();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        poco.Create<ISecretCommand>().CrisPocoModel.HasSensitiveData.ShouldBeTrue();
        poco.Create<IWrapperCommand>().CrisPocoModel.HasSensitiveData.ShouldBeTrue( "Its Inner may be a ISecretCommand." );
        poco.Create<IPlainCommand>().CrisPocoModel.HasSensitiveData.ShouldBeFalse();
    }

    [Test]
    public async Task TransformSensitiveStrings_traverses_the_nested_Cris_Pocos_Async()
    {
        await using var auto = await CreateAsync();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var secret = poco.Create<ISecretCommand>( c => { c.Secret = "password"; c.Public = "visible"; } );
        var wrapper = poco.Create<IWrapperCommand>( c => c.Inner = secret );

        var seen = new List<string>();
        wrapper.TransformSensitiveStrings( ( owner, name, value ) =>
        {
            seen.Add( $"{owner.CrisPocoModel.PocoName}.{name}={value}" );
            return value.ToUpperInvariant();
        } ).ShouldBeTrue();
        seen.ShouldHaveSingleItem().ShouldEndWith( "ISecretCommand.Secret=password" );
        secret.Secret.ShouldBe( "PASSWORD" );
        secret.Public.ShouldBe( "visible" );

        // A null result stops the transformation: it fails (a failed decryption).
        wrapper.TransformSensitiveStrings( ( owner, name, value ) => null ).ShouldBeFalse();
        secret.Secret.ShouldBe( "PASSWORD" );

        // Null values are skipped, a plain Poco has nothing to transform.
        poco.Create<IWrapperCommand>().TransformSensitiveStrings( ( owner, name, value ) => null ).ShouldBeTrue();
        poco.Create<IPlainCommand>().TransformSensitiveStrings( ( owner, name, value ) => null ).ShouldBeTrue();
    }

    [Test]
    public async Task a_sensitive_Poco_is_logged_redacted_and_is_not_modified_Async()
    {
        // The redacted copy needs the Poco JSON exchange (as in any Cris application that exchanges commands).
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( CommonPocoJsonSupport ),
                                              typeof( ISecretCommand ), typeof( IWrapperCommand ), typeof( IPlainCommand ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var secret = poco.Create<ISecretCommand>( c => { c.Secret = "password"; c.Public = "visible"; } );
        var wrapper = poco.Create<IWrapperCommand>( c => c.Inner = secret );

        var log = wrapper.ToLogString();
        log.ShouldContain( SensitiveDataExtensions.Redacted );
        log.ShouldContain( "visible" );
        log.ShouldNotContain( "password" );
        secret.Secret.ShouldBe( "password" );

        var plain = poco.Create<IPlainCommand>( c => c.Name = "Albert" );
        plain.ToLogString().ShouldBe( plain.ToString() );
    }

    [LocalEvent]
    public interface ISecretEvent : IEvent
    {
        [Sensitive]
        string Secret { get; set; }
    }

    public sealed class ThrowingEventHandler : IAutoService
    {
        [LocalEventHandler]
        public void On( ISecretEvent e ) => throw new System.InvalidOperationException( "Handler failure." );
    }

    [Test]
    public async Task a_failed_safe_event_dispatch_logs_the_event_redacted_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RawCrisExecutor ), typeof( CrisDirectory ), typeof( CommonPocoJsonSupport ), typeof( ISecretEvent ), typeof( ThrowingEventHandler ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices( configureServices: services =>
        {
            services.AddScoped<IActivityMonitor, ActivityMonitor>();
            services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
        } );
        using var scope = auto.Services.CreateScope();
        var monitor = scope.ServiceProvider.GetRequiredService<IActivityMonitor>();
        monitor.MinimalFilter = LogFilter.Trace;
        var e = scope.ServiceProvider.GetRequiredService<PocoDirectory>().Create<ISecretEvent>( e => e.Secret = "password" );
        using( monitor.CollectTexts( out var logs ) )
        {
            (await scope.ServiceProvider.GetRequiredService<RawCrisExecutor>().SafeDispatchEventAsync( scope.ServiceProvider, e )).ShouldBeFalse();
            logs.ShouldContain( l => l.Contains( SensitiveDataExtensions.Redacted ) );
            logs.ShouldNotContain( l => l.Contains( "password" ) );
        }
        e.Secret.ShouldBe( "password" );
    }

    public interface IApiCredential : IPoco
    {
        [Sensitive]
        string Secret { get; set; }
    }

    public interface ICredentialCommand : ICommand
    {
        IApiCredential Credential { get; }
    }

    public interface ISecretListCommand : ICommand
    {
        IList<ISecretCommand> Secrets { get; }
    }

    public interface IPlainListCommand : ICommand
    {
        IList<IPlainCommand> Commands { get; }
    }

    // A collection of plain IPocos is not traversed.
    public interface ISecretCredentialsCommand : ICommand
    {
        IList<IApiCredential> Credentials { get; }
    }

    // Like the Events of an executed command: the items of a list of Cris Pocos are traversed.
    [Test]
    public async Task a_list_of_Cris_Pocos_is_traversed_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( CommonPocoJsonSupport ), typeof( ISecretCommand ),
                                              typeof( IWrapperCommand ), typeof( ISecretListCommand ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var secret = poco.Create<ISecretCommand>( c => { c.Secret = "password"; c.Public = "visible"; } );
        var list = poco.Create<ISecretListCommand>( c => c.Secrets.Add( secret ) );
        list.CrisPocoModel.HasSensitiveData.ShouldBeTrue();

        var log = list.ToLogString();
        log.ShouldContain( SensitiveDataExtensions.Redacted );
        log.ShouldContain( "visible" );
        log.ShouldNotContain( "password" );
        secret.Secret.ShouldBe( "password" );

        list.TransformSensitiveStrings( ( owner, name, value ) => null ).ShouldBeFalse( "The item's secret is transformed." );
    }

    // Like a batch of commands: Cris Pocos in value tuples and dictionary values are traversed.
    public interface IBatchCommand : ICommand
    {
        IList<(IAbstractCommand Command, string? Description)> Commands { get; }

        IDictionary<string, IAbstractCommand> Named { get; }
    }

    [Test]
    public async Task value_tuples_and_dictionary_values_are_traversed_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( CommonPocoJsonSupport ), typeof( ISecretCommand ), typeof( IBatchCommand ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var inTuple = poco.Create<ISecretCommand>( c => { c.Secret = "password1"; c.Public = "visible"; } );
        var inDictionary = poco.Create<ISecretCommand>( c => c.Secret = "password2" );
        var batch = poco.Create<IBatchCommand>( c =>
        {
            c.Commands.Add( (inTuple, "The description.") );
            c.Named.Add( "N", inDictionary );
        } );
        batch.CrisPocoModel.HasSensitiveData.ShouldBeTrue();

        var log = batch.ToLogString();
        log.ShouldContain( "visible" );
        log.ShouldContain( "The description." );
        log.ShouldNotContain( "password1" );
        log.ShouldNotContain( "password2" );

        var seen = new List<string>();
        batch.TransformSensitiveStrings( ( owner, name, value ) => { seen.Add( value ); return value; } ).ShouldBeTrue();
        seen.ShouldBe( ["password1", "password2"], ignoreOrder: true );
    }

    [Test]
    public async Task unhandled_Sensitive_values_are_errors_Async()
    {
        var c1 = TestHelper.CreateDefaultEngineConfiguration();
        c1.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IApiCredential ), typeof( ICredentialCommand ) );
        await c1.GetFailedAutomaticServicesAsync( "'CK.Cris.Executor.Tests.SensitiveDataTests.ICredentialCommand.Credential' may hold [Sensitive] values that are not handled" );

        var c2 = TestHelper.CreateDefaultEngineConfiguration();
        c2.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IApiCredential ), typeof( ISecretCredentialsCommand ) );
        await c2.GetFailedAutomaticServicesAsync( "'CK.Cris.Executor.Tests.SensitiveDataTests.ISecretCredentialsCommand.Credentials' may hold [Sensitive] values that are not handled" );

        // A collection of Cris Pocos without sensitive data is fine.
        var c3 = TestHelper.CreateDefaultEngineConfiguration();
        c3.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IPlainCommand ), typeof( IPlainListCommand ) );
        await using var auto = (await c3.RunSuccessfullyAsync()).CreateAutomaticServices();
    }

    [Test]
    public async Task Sensitive_applies_to_strings_only_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IBadSensitiveCommand ) );
        await configuration.GetFailedAutomaticServicesAsync( "must be a string." );
    }

    [Test]
    public async Task without_the_Json_exchange_a_sensitive_Poco_is_still_redacted_Async()
    {
        // The redacted copy is a Poco Clone(): it doesn't need the Json exchange (the logged string is then the
        // ToString() of the copy).
        await using var auto = await CreateAsync();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var secret = poco.Create<ISecretCommand>( c => c.Secret = "password" );
        secret.ToLogString().ShouldNotContain( "password" );
        secret.Secret.ShouldBe( "password" );
    }
}
