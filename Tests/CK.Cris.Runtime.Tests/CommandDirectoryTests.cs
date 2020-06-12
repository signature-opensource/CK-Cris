using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.Tests
{
    [TestFixture]
    public class CommandDirectoryTests
    {
        [CommandName( "Test", "PreviousTest1", "PreviousTest2" )]
        public interface ICmdTest : ICommand
        {
        }

        [Test]
        public void simple_command_models()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICmdTest ) );
            var d = TestHelper.GetAutomaticServices( c ).Services.GetRequiredService<CommandDirectory>();
            d.Commands.Should().HaveCount( 1 );
            var m = d.FindModel( "Test" );
            m.Should().NotBeNull();
            d.Commands[0].Should().BeSameAs( m );
            m.CommandIdx.Should().Be( 0 );
            m.CommandName.Should().Be( "Test" );
            m.PreviousNames.Should().BeEquivalentTo( "PreviousTest1", "PreviousTest2" );
            m.CommandType.Should().Be( typeof(ICmdTest) );
            m.Should().BeSameAs( d.FindModel( "PreviousTest1" ) ).And.BeSameAs( d.FindModel( "PreviousTest2" ) );
            var cmd = m.CreateInstance();
            d.FindModel( cmd ).Should().BeSameAs( m );
        }

    }
}
