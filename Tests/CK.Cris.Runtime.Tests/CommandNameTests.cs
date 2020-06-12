using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.Tests
{
    [TestFixture]
    public class CommandNameTests
    {
        [CommandName( "Test", "Prev1", "Test" )]
        public interface ICmdBadName1 : ICommand { }

        [CommandName( "Test", "Test" )]
        public interface ICmdBadName2 : ICommand { }

        [Test]
        public void duplicate_names_on_the_command_are_errors()
        {
            {
                var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICmdBadName1 ) );
                using( TestHelper.Monitor.CollectEntries( entries => entries.Should()
                                                .Match( e => e.Any( x => x.MaskedLevel == LogLevel.Error
                                                                         && x.Text.StartsWith( "Duplicate CommandName in attribute " ) ) ) ) )
                {
                    TestHelper.GenerateCode( c ).CodeGenResult.Success.Should().BeFalse();
                }
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICmdBadName2 ) );
                using( TestHelper.Monitor.CollectEntries( entries => entries.Should()
                                                .Match( e => e.Any( x => x.MaskedLevel == LogLevel.Error
                                                                         && x.Text.StartsWith( "Duplicate CommandName in attribute " ) ) ) ) )
                {
                    TestHelper.GenerateCode( c ).CodeGenResult.Success.Should().BeFalse();
                }
            }
        }

        public interface ICmdNoName : ICommand { }

        public interface ICmdNoNameA : ICmdNoName { }

        public interface ICmdNoNameB : ICmdNoName { }

        public interface ICmdNoNameC : ICmdNoNameA, ICmdNoNameB { }

        [Test]
        public void no_CommandName_uses_PrimaryInterface_FullName()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICmdNoName ), typeof( ICmdNoNameA ), typeof( ICmdNoNameB ), typeof( ICmdNoNameC ) );
            using( TestHelper.Monitor.CollectEntries( entries => entries.Should()
                                            .Match( e => e.Any( x => x.MaskedLevel == LogLevel.Warn
                                                                     && x.Text.StartsWith( $"Command '{typeof( ICmdNoName ).FullName}' use its full name " ) ) ) ) )
            {
                TestHelper.GenerateCode( c ).CodeGenResult.Success.Should().BeTrue();
            }
        }

        [CommandName( "NoWay" )]
        public interface ICmdSecondary : ICmdNoName
        {
        }

        [Test]
        public void CommandName_attribute_must_be_on_the_primary_interface()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICmdSecondary ) );
            using( TestHelper.Monitor.CollectEntries( entries => entries.Should()
                                            .Match( e => e.Any( x => x.MaskedLevel == LogLevel.Error
                                                                     && x.Text.StartsWith( $"CommandName attribute appear on '{typeof(ICmdSecondary).FullName}'." ) ) ) ) )
            {
                TestHelper.GenerateCode( c ).CodeGenResult.Success.Should().BeFalse();
            }
        }

        [CommandName( "Cmd1" )]
        public interface ICmd1 : ICommand
        {
        }

        [CommandName( "Cmd1" )]
        public interface ICmd1Bis : ICommand
        {
        }

        [Test]
        public void CommandName_must_be_unique()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICmd1 ), typeof( ICmd1Bis ) );
            using( TestHelper.Monitor.CollectEntries( entries => entries.Should()
                                            .Match( e => e.Any( x => x.MaskedLevel == LogLevel.Error
                                                                     && x.Text.StartsWith( "The command name 'Cmd1' clashes: both '" ) ) ) ) )
            {
                TestHelper.GenerateCode( c ).CodeGenResult.Success.Should().BeFalse();
            }
        }

    }
}
