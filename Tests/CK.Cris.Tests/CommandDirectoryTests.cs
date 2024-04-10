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
    public class CrisDirectoryTests
    {
        [ExternalName( "Test", "PreviousTest1", "PreviousTest2" )]
        public interface ICmdTest : ICommand
        {
        }

        [Test]
        public void simple_command_models()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), typeof( ICmdTest ) );
            using var services = TestHelper.CreateAutomaticServices( c ).Services;
            var poco = services.GetRequiredService<PocoDirectory>();

            var d = services.GetRequiredService<CrisDirectory>();
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

        public interface ICmdTestSpec : ICmdTest, IEvent
        {
        }

        [Test]
        public void IEvent_cannot_be_a_ICommand()
        {
            using( TestHelper.Monitor.CollectTexts( out var texts ) )
            {
                var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), typeof( ICmdTestSpec ) );
                TestHelper.GenerateCode( c, null ).Success.Should().BeFalse();
                texts.Should().Contain( "Cris '[PrimaryPoco]CK.Cris.Tests.CrisDirectoryTests.ICmdTest' cannot be both a IEvent and a IAbstractCommand." );
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
                var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), typeof( ICmdNoWay ) );
                TestHelper.GenerateCode( c, null ).Success.Should().BeFalse();
                texts.Should().Contain( "Cris '[PrimaryPoco]CK.Cris.Tests.CrisDirectoryTests.ICmdNoWay' cannot be both a IEvent and a IAbstractCommand." );
            }
        }
    }
}

