using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.Tests;

[TestFixture]
public class CommandResultTypeTests
{
    public interface IIntCommand : ICommand<int>
    {
    }

    [Test]
    public async Task simple_basic_return_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IIntCommand ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<CrisDirectory>();
        d.CrisPocoModels[0].ResultType.ShouldBe( typeof( int ) );
    }

    public interface IIntButObjectCommand : IIntCommand, ICommand<object>
    {
    }

    [Test]
    public async Task a_specialized_command_can_generalize_the_initial_result_type_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IIntButObjectCommand ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var d = auto.Services.GetRequiredService<CrisDirectory>();
        var cmdModel = d.CrisPocoModels[0];
        cmdModel.CommandType.ShouldBeAssignableTo( typeof( IIntButObjectCommand ) );
        cmdModel.ResultType.ShouldBe( typeof( int ) );
    }

    public interface IIntButStringCommand : IIntCommand, ICommand<string>
    {
    }

    [Test]
    public async Task incompatible_result_type_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IIntButStringCommand ) );
        await configuration.GetFailedAutomaticServicesAsync(
            "Command '[PrimaryPoco]CK.Cris.Tests.CommandResultTypeTests.IIntCommand' declares incompatible results '[AbstractPoco]CK.Cris.ICommand<int>' ,'[AbstractPoco]CK.Cris.ICommand<string>': result types are incompatible and cannot be reduced." );
    }

    [Test]
    public async Task when_IPoco_result_are_not_closed_this_is_invalid_Async()
    {
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IWithMorePocoResultCommand ), typeof( IMoreResult ) );
            await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
            var d = auto.Services.GetRequiredService<CrisDirectory>();
            var cmdModel = d.CrisPocoModels[0];
            cmdModel.CommandType.ShouldBeAssignableTo( typeof( IWithMorePocoResultCommand ) );
            cmdModel.ResultType.ShouldBeAssignableTo( typeof( IMoreResult ) );
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IWithAnotherPocoResultCommand ), typeof( IAnotherResult ) );
            await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
            var d = auto.Services.GetRequiredService<CrisDirectory>();
            var cmdModel = d.CrisPocoModels[0];
            cmdModel.CommandType.ShouldBeAssignableTo( typeof( IWithAnotherPocoResultCommand ) );
            cmdModel.ResultType.ShouldBeAssignableTo( typeof( IAnotherResult ) );
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IUnifiedButNotTheResultCommand ), typeof( IMoreResult ), typeof( IAnotherResult ) );
            await configuration.GetFailedAutomaticServicesAsync(
                "Command '[PrimaryPoco]CK.Cris.Tests.IWithPocoResultCommand' declares incompatible results '[AbstractPoco]CK.Cris.ICommand<CK.Cris.Tests.IMoreResult>' ,'[AbstractPoco]CK.Cris.ICommand<CK.Cris.Tests.IAnotherResult>': result types are incompatible and cannot be reduced." );
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IWithTheResultUnifiedCommand ), typeof( IUnifiedResult ) );
            await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
            var d = auto.Services.GetRequiredService<CrisDirectory>();
            var cmdModel = d.CrisPocoModels[0];
            cmdModel.CommandType.ShouldBeAssignableTo( typeof( IWithTheResultUnifiedCommand ) );
            cmdModel.ResultType.ShouldBeAssignableTo( typeof( IUnifiedResult ) );
        }
    }

}
