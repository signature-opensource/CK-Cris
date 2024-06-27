using CK.Auth;
using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.BackgroundExecutor.Tests
{
    [TestFixture]
    public class ExecutingCommandTests
    {
        public interface IMyCommandResult : IStandardResultPart
        {
            int Power { get; set; }
        }

        public interface IMyCommand : ICommand<IMyCommandResult>
        {
            int WantedPower { get; set; }
        }

        public sealed class MyHandler : IRealObject
        {
            [CommandHandler]
            public IMyCommandResult Process( CurrentCultureInfo culture, IMyCommand command )
            {
                var r = command.CreateResult();
                r.Power = command.WantedPower / 2;
                return r;
            }
        }

        [Test]
        public async Task using_scoped_CrisBackgroundExecutor_is_simple_Async()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisBackgroundExecutorService ),
                                                  typeof( IMyCommand ),
                                                  typeof( IMyCommandResult ),
                                                  typeof( MyHandler ),
                                                  typeof( CrisBackgroundExecutorService ),
                                                  typeof( CrisBackgroundExecutor ) );
            using var auto = configuration.RunSuccessfully().CreateAutomaticServices();

            using var scoped = auto.Services.CreateScope();
            var poco = scoped.ServiceProvider.GetRequiredService<PocoDirectory>();
            var executor = scoped.ServiceProvider.GetRequiredService<CrisBackgroundExecutor>();
            var cmd = poco.Create<IMyCommand>( c => c.WantedPower = 3712 );
            var ec = executor.Submit( TestHelper.Monitor, cmd ).WithResult<IMyCommandResult>();

            var r = await ec.Result;
            r.Power.Should().Be( 1856 );
        }


        public interface IMyExtendedCommandResult : IMyCommandResult
        {
            int AnotherPower { get; set; }
        }

        public sealed class MyExtendedHandler : IRealObject
        {
            [CommandHandler]
            public IMyExtendedCommandResult Process( CurrentCultureInfo culture, IMyExtendedCommand command )
            {
                var r = command.CreateResult<IMyExtendedCommandResult>();
                r.Power = command.WantedPower / 2;
                r.AnotherPower = command.SomeOtherStuff.GetHashCode();
                return r;
            }
        }

        [Test]
        public async Task ExecutingCommand_handles_different_Result_type_Async()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CrisBackgroundExecutorService ),
                                                  typeof( IMyExtendedCommand ),
                                                  typeof( IMyExtendedCommandResult ),
                                                  typeof( MyExtendedHandler ),
                                                  typeof( CrisBackgroundExecutorService ),
                                                  typeof( CrisBackgroundExecutor ) );
            using var auto = configuration.RunSuccessfully().CreateAutomaticServices();

            using var scoped = auto.Services.CreateScope();
            var poco = scoped.ServiceProvider.GetRequiredService<PocoDirectory>();
            var executor = scoped.ServiceProvider.GetRequiredService<CrisBackgroundExecutor>();

            var cmd = poco.Create<IMyCommand>( c => c.WantedPower = 42 );

            ((IMyExtendedCommand)cmd).SomeOtherStuff = "";

            var ec = executor.Submit( TestHelper.Monitor, cmd ).WithResult<IMyCommandResult>();

            var ec2 = ec.WithResult<IMyExtendedCommandResult>();
            var r = await ec.Result;
            r.Power.Should().Be( 21 );

            var r2 = await ec2.Result;
            r2.Power.Should().Be( 21 );
            r2.AnotherPower.Should().Be( "".GetHashCode() );

            ec.WithResult<IMyCommandResult>().Should().BeSameAs( ec );
            ec2.WithResult<IMyExtendedCommandResult>().Should().BeSameAs( ec2 );

            FluentActions.Invoking( () => ec.WithResult<string>() )
                .Should().Throw<ArgumentException>(); 
        }



        public interface IMyExtendedCommand : ICommand<IMyExtendedCommandResult>, IMyCommand
        {
            string SomeOtherStuff { get; set; }
        }


    }
}
