using CK.Core;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.Tests
{
    [TestFixture]
    public class CommandDirectoryTests
    {
        [ExternalName( "Test", "PreviousTest1", "PreviousTest2" )]
        public interface ICmdTest : ICommand
        {
        }

        [Test]
        public void simple_command_models()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICmdTest ), typeof( AmbientValues.IAmbientValues ) );
            using var services = TestHelper.CreateAutomaticServices( c ).Services;
            var poco = services.GetRequiredService<PocoDirectory>();

            var d = services.GetRequiredService<CommandDirectory>();
            d.Commands.Should().HaveCount( 1 );
            var m = d.Commands[0];
            m.Handler.Should().BeNull();
            m.CommandIdx.Should().Be( 0 );
            m.CommandName.Should().Be( "Test" );
            m.PreviousNames.Should().BeEquivalentTo( "PreviousTest1", "PreviousTest2" );
            m.Should().BeSameAs( poco.Find( "PreviousTest1" ) ).And.BeSameAs( poco.Find( "PreviousTest2" ) );
            var cmd = m.Create();
            cmd.CommandModel.Should().BeSameAs( m );
        }

        public interface ICmdTestSpec : ICmdTest, ICrisEvent
        {
        }

        [Test]
        public void ICrisEvent_cannot_be_a_ICommand()
        {
            using( TestHelper.Monitor.CollectTexts( out var texts ) )
            {
                var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICmdTestSpec ), typeof( AmbientValues.IAmbientValues ) );
                TestHelper.GenerateCode( c, null ).Success.Should().BeFalse();
                texts.Should().Contain( "Command 'Test' cannot be both a ICrisEvent and a ICommand." );
            }
        }

        public interface ICmdNoWay : ICrisEvent, ICommand<int>
        {
        }

        [Test]
        public void ICrisEvent_cannot_be_a_ICommand_TResult()
        {
            using( TestHelper.Monitor.CollectTexts( out var texts ) )
            {
                var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICmdNoWay ), typeof( AmbientValues.IAmbientValues ) );
                TestHelper.GenerateCode( c, null ).Success.Should().BeFalse();
                texts.Should().Contain( "Command 'CK.Cris.Tests.CommandDirectoryTests+ICmdNoWay' cannot be both a ICrisEvent and a ICommand<TResult>." );
            }
        }
    }
}

