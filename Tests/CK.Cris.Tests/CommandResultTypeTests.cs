using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.Tests
{
    [TestFixture]
    public class CommandResultTypeTests
    {
        public interface IIntCommand : ICommand<int>
        {
        }

        [Test]
        public void simple_basic_return()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IIntCommand ) );
            using var auto = configuration.RunSuccessfully().CreateAutomaticServices();

            var d = auto.Services.GetRequiredService<CrisDirectory>();
            d.CrisPocoModels[0].ResultType.Should().Be( typeof( int ) );
        }

        public interface IIntButObjectCommand : IIntCommand, ICommand<object>
        {
        }

        [Test]
        public void a_specialized_command_can_generalize_the_initial_result_type()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IIntButObjectCommand ) );
            using var auto = configuration.RunSuccessfully().CreateAutomaticServices();
            var d = auto.Services.GetRequiredService<CrisDirectory>();
            var cmdModel = d.CrisPocoModels[0];
            cmdModel.CommandType.Should().BeAssignableTo( typeof( IIntButObjectCommand ) );
            cmdModel.ResultType.Should().Be( typeof( int ) );
        }

        public interface IIntButStringCommand : IIntCommand, ICommand<string>
        {
        }

        [Test]
        public void incompatible_result_type()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IIntButStringCommand ) );
            configuration.GetFailedAutomaticServices(
                "Command '[PrimaryPoco]CK.Cris.Tests.CommandResultTypeTests.IIntCommand' declares incompatible results '[AbstractPoco]CK.Cris.ICommand<int>' ,'[AbstractPoco]CK.Cris.ICommand<string>': result types are incompatible and cannot be reduced." );
        }

        [Test]
        public void when_IPoco_result_are_not_closed_this_is_invalid()
        {
            {
                var configuration = TestHelper.CreateDefaultEngineConfiguration();
                configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IWithMorePocoResultCommand ), typeof( IMoreResult ) );
                using var auto = configuration.RunSuccessfully().CreateAutomaticServices();
                var d = auto.Services.GetRequiredService<CrisDirectory>();
                var cmdModel = d.CrisPocoModels[0];
                cmdModel.CommandType.Should().BeAssignableTo( typeof( IWithMorePocoResultCommand ) );
                cmdModel.ResultType.Should().BeAssignableTo( typeof( IMoreResult ) );
            }
            {
                var configuration = TestHelper.CreateDefaultEngineConfiguration();
                configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IWithAnotherPocoResultCommand ), typeof( IAnotherResult ) );
                using var auto = configuration.RunSuccessfully().CreateAutomaticServices();
                var d = auto.Services.GetRequiredService<CrisDirectory>();
                var cmdModel = d.CrisPocoModels[0];
                cmdModel.CommandType.Should().BeAssignableTo( typeof( IWithAnotherPocoResultCommand ) );
                cmdModel.ResultType.Should().BeAssignableTo( typeof( IAnotherResult ) );
            }
            {
                var configuration = TestHelper.CreateDefaultEngineConfiguration();
                configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IUnifiedButNotTheResultCommand ), typeof( IMoreResult ), typeof( IAnotherResult ) );
                configuration.GetFailedAutomaticServices(
                    "Command '[PrimaryPoco]CK.Cris.Tests.IWithPocoResultCommand' declares incompatible results '[AbstractPoco]CK.Cris.ICommand<CK.Cris.Tests.IMoreResult>' ,'[AbstractPoco]CK.Cris.ICommand<CK.Cris.Tests.IAnotherResult>': result types are incompatible and cannot be reduced." );
            }
            {
                var configuration = TestHelper.CreateDefaultEngineConfiguration();
                configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ), typeof( IWithTheResultUnifiedCommand ), typeof( IUnifiedResult ) );
                using var auto = configuration.RunSuccessfully().CreateAutomaticServices();
                var d = auto.Services.GetRequiredService<CrisDirectory>();
                var cmdModel = d.CrisPocoModels[0];
                cmdModel.CommandType.Should().BeAssignableTo( typeof( IWithTheResultUnifiedCommand ) );
                cmdModel.ResultType.Should().BeAssignableTo( typeof( IUnifiedResult ) );
            }
        }



    }
}
