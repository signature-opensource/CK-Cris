using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
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
    public void simple_command_models()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( ITestCommand ) );
        using var auto = configuration.RunSuccessfully().CreateAutomaticServices();

        var poco = auto.Services.GetRequiredService<PocoDirectory>();

        var d = auto.Services.GetRequiredService<CrisDirectory>();
        d.CrisPocoModels.Should().HaveCount( 1 );
        var m = d.CrisPocoModels[0];
        m.Handlers.Should().BeEmpty();
        m.CrisPocoIndex.Should().Be( 0 );
        m.PocoName.Should().Be( "Test" );
        m.PreviousNames.Should().BeEquivalentTo( "PreviousTest1", "PreviousTest2" );
        m.Should().BeSameAs( poco.Find( "PreviousTest1" ) ).And.BeSameAs( poco.Find( "PreviousTest2" ) );
        var cmd = m.Create();
        cmd.CrisPocoModel.Should().BeSameAs( m );
    }

    public interface ITestSpecCommand : ITestCommand, IEvent
    {
    }

    [Test]
    public void IEvent_cannot_be_a_ICommand()
    {
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( ITestSpecCommand ) );
            configuration.GetFailedAutomaticServices(
                "Cris '[PrimaryPoco]CK.Cris.Tests.CrisDirectoryTests.ITestCommand' cannot be both a IEvent and a IAbstractCommand." );
        }
    }

    public interface ICmdNoWay : IEvent, ICommand<int>
    {
    }

    [Test]
    public void IEvent_cannot_be_a_ICommand_TResult()
    {
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( ICmdNoWay ) );
            configuration.GetFailedAutomaticServices(
                "Cris '[PrimaryPoco]CK.Cris.Tests.CrisDirectoryTests.ICmdNoWay' cannot be both a IEvent and a IAbstractCommand." );
        }
    }

}

