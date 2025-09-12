using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.Tests;


[TestFixture]
public class CrisDirectoryTests
{
    [ExternalName( "Test", "PreviousTest1", "PreviousTest2" )]
    public interface ITestCommand : ICommand
    {
    }

    [Test]
    public async Task simple_command_models_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( ITestCommand ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();

        var poco = auto.Services.GetRequiredService<PocoDirectory>();

        var d = auto.Services.GetRequiredService<CrisDirectory>();
        d.CrisPocoModels.Count.ShouldBe( 1 );
        var m = d.CrisPocoModels[0];
        m.Handlers.ShouldBeEmpty();
        m.CrisPocoIndex.ShouldBe( 0 );
        m.PocoName.ShouldBe( "Test" );
        m.PreviousNames.ShouldBe( ["PreviousTest1", "PreviousTest2"], ignoreOrder: true );
        m.ShouldBeSameAs( poco.Find( "PreviousTest1" ) ).ShouldBeSameAs( poco.Find( "PreviousTest2" ) );
        var cmd = m.Create();
        cmd.CrisPocoModel.ShouldBeSameAs( m );
    }

    public interface ITestSpecCommand : ITestCommand, IEvent
    {
    }

    [Test]
    public async Task IEvent_cannot_be_a_ICommand_Async()
    {
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( ITestSpecCommand ) );
            await configuration.GetFailedAutomaticServicesAsync(
                "Cris '[PrimaryPoco]CK.Cris.Tests.CrisDirectoryTests.ITestCommand' cannot be both a IEvent and a IAbstractCommand." );
        }
    }

    public interface ICmdNoWay : IEvent, ICommand<int>
    {
    }

    [Test]
    public async Task IEvent_cannot_be_a_ICommand_TResult_Async()
    {
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( ICmdNoWay ) );
            await configuration.GetFailedAutomaticServicesAsync(
                "Cris '[PrimaryPoco]CK.Cris.Tests.CrisDirectoryTests.ICmdNoWay' cannot be both a IEvent and a IAbstractCommand." );
        }
    }

}

