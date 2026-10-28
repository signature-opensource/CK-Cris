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

    [Test]
    public async Task Sensitive_applies_to_strings_only_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IBadSensitiveCommand ) );
        await configuration.GetFailedAutomaticServicesAsync( "must be a string." );
    }

    [Test]
    public async Task without_the_Json_exchange_a_sensitive_Poco_is_not_logged_Async()
    {
        await using var auto = await CreateAsync();
        var poco = auto.Services.GetRequiredService<PocoDirectory>();
        var secret = poco.Create<ISecretCommand>( c => c.Secret = "password" );
        secret.ToLogString().ShouldBe( $"{secret.CrisPocoModel.PocoName} (sensitive content not logged)" );
    }
}
