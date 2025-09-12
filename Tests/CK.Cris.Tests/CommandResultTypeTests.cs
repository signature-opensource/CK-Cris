using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
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

    public class IntCommandHandler : IAutoService
    {
        [CommandHandler]
        public int HandleIntCommand( IIntCommand cmd ) => 3712;
    }


    [Test]
    public async Task simple_basic_return_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IIntCommand ), typeof( IntCommandHandler ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<CrisDirectory>();
        d.CrisPocoModels[0].ResultType.ShouldBe( typeof( int ) );
        d.CrisPocoModels[0].IsHandled.ShouldBeTrue();
        d.CrisPocoModels[0].Handlers[0].MethodName.ShouldBe( "HandleIntCommand" );
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
        cmdModel.CommandType.IsAssignableTo( typeof( IIntButObjectCommand ) ).ShouldBeTrue();
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
            """
            Command '[PrimaryPoco]CK.Cris.Tests.CommandResultTypeTests.IIntCommand' declares incompatible results '[AbstractPoco]CK.Cris.ICommand<int>' ,'[AbstractPoco]CK.Cris.ICommand<string>'.
            Result types are incompatible and cannot be reduced.
            """ );
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
            cmdModel.CommandType.IsAssignableTo( typeof( IWithMorePocoResultCommand ) ).ShouldBeTrue();
            cmdModel.ResultType.IsAssignableTo( typeof( IMoreResult ) ).ShouldBeTrue();
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IWithAnotherPocoResultCommand ), typeof( IAnotherResult ) );
            await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
            var d = auto.Services.GetRequiredService<CrisDirectory>();
            var cmdModel = d.CrisPocoModels[0];
            cmdModel.CommandType.IsAssignableTo( typeof( IWithAnotherPocoResultCommand ) ).ShouldBeTrue();
            cmdModel.ResultType.IsAssignableTo( typeof( IAnotherResult ) ).ShouldBeTrue();
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IUnifiedButNotTheResultCommand ), typeof( IMoreResult ), typeof( IAnotherResult ) );
            await configuration.GetFailedAutomaticServicesAsync(
                """
                Command '[PrimaryPoco]CK.Cris.Tests.IWithPocoResultCommand' declares incompatible results '[AbstractPoco]CK.Cris.ICommand<CK.Cris.Tests.IMoreResult>' ,'[AbstractPoco]CK.Cris.ICommand<CK.Cris.Tests.IAnotherResult>'.
                Result types are incompatible and cannot be reduced.
                """ );
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IWithTheResultUnifiedCommand ), typeof( IUnifiedResult ) );
            await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
            var d = auto.Services.GetRequiredService<CrisDirectory>();
            var cmdModel = d.CrisPocoModels[0];
            cmdModel.CommandType.IsAssignableTo( typeof( IWithTheResultUnifiedCommand ) ).ShouldBeTrue();
            cmdModel.ResultType.IsAssignableTo( typeof( IUnifiedResult ) ).ShouldBeTrue();
        }
    }

    public interface IMultiCommandsCommand : ICommand<IList<IIntCommand>>
    {
    }

    public class CommandOfCommandsHandler : IAutoService
    {
        [CommandHandler]
        public IList<IIntCommand> CreateMultipleCommand( IMultiCommandsCommand cmd ) => [];
    }

    [Test]
    public async Task command_return_List_of_Ref_Type_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ),
                                                typeof( IMultiCommandsCommand ),
                                                typeof( IIntCommand ),
                                                typeof( CommandOfCommandsHandler ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<CrisDirectory>();
        directory.CrisPocoModels.Count.ShouldBe( 2 );
        directory.CrisPocoModels.Single( m => m.CommandType.Name == "CommandResultTypeTests_IIntCommand_CK" ).IsHandled.ShouldBeFalse( "IIntCommand is not handled." );
        var multi = directory.CrisPocoModels.Single( m => m.CommandType.Name == "CommandResultTypeTests_IMultiCommandsCommand_CK" );
        multi.IsHandled.ShouldBeTrue();
        multi.Handlers.Length.ShouldBe( 1 );
        multi.Handlers[0].MethodName.ShouldBe( "CreateMultipleCommand" );
    }

    // To be investigated.
    //[Test]
    //public async Task command_with_unregistered_returned_Types_is_an_error_Async()
    //{
    //    // We don't register IIntCommand here.
    //    var configuration = TestHelper.CreateDefaultEngineConfiguration();
    //    configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ),
    //                                            typeof( IMultiCommandsCommand ),
    //                                            typeof( CommandOfCommandsHandler ) );
    //    await configuration.GetFailedAutomaticServicesAsync(
    //        """
    //        Command '[PrimaryPoco]CK.Cris.Tests.CommandResultTypeTests.IMultiCommandsCommand' has at least one unregistered type:
    //        [List]IList<CK.Cris.Tests.CommandResultTypeTests.IIntCommand>.
    //        """ );
    //}

}
