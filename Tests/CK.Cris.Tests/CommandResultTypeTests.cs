using CK.Core;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    public class CommandResultTypeTests
    {
        public interface ICInt : ICommand<int>
        {
        }

        [Test]
        public void simple_basic_return()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICInt ) );
            var d = TestHelper.GetAutomaticServices( c ).Services.GetRequiredService<CommandDirectory>();
            d.Commands[0].ResultType.Should().Be( typeof( int ) );
        }

        public interface ICIntButObject : ICInt, ICommand<object>
        {
        }

        [Test]
        public void a_specialized_command_can_generalize_the_initial_result_type()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICIntButObject ) );
            var d = TestHelper.GetAutomaticServices( c ).Services.GetRequiredService<CommandDirectory>();
            var cmdModel = d.Commands[0];
            cmdModel.CommandType.Should().BeAssignableTo( typeof( ICIntButObject ) );
            cmdModel.ResultType.Should().Be( typeof( int ) );
        }

        public interface ICIntButString : ICInt, ICommand<string>
        {
        }

        [Test]
        public void incompatible_result_type()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICIntButString ) );
            TestHelper.GenerateCode( c ).CodeGen.Success.Should().BeFalse();
            //=> Invalid command Result type for 'CK.Cris.Tests.CommandResultTypeTests+ICInt': result types 'Int32', 'String' must resolve to a common most specific type.
        }

        [Test]
        public void when_IPoco_result_are_not_closed_this_is_invalid()
        {
            {
                var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICommandWithMorePocoResult ), typeof( IMoreResult ) );
                var d = TestHelper.GetAutomaticServices( c ).Services.GetRequiredService<CommandDirectory>();
                var cmdModel = d.Commands[0];
                cmdModel.CommandType.Should().BeAssignableTo( typeof( ICommandWithMorePocoResult ) );
                cmdModel.ResultType.Should().BeAssignableTo( typeof( IMoreResult ) );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICommandWithAnotherPocoResult ), typeof( IAnotherResult ) );
                var d = TestHelper.GetAutomaticServices( c ).Services.GetRequiredService<CommandDirectory>();
                var cmdModel = d.Commands[0];
                cmdModel.CommandType.Should().BeAssignableTo( typeof( ICommandWithAnotherPocoResult ) );
                cmdModel.ResultType.Should().BeAssignableTo( typeof( IAnotherResult ) );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICommandUnifiedButNotTheResult ), typeof( IMoreResult ), typeof( IAnotherResult ) );
                var d = TestHelper.GenerateCode( c ).CodeGen.Success.Should().BeFalse();
                //=> Invalid command Result type for 'CK.Cris.Tests.CommandResultTypeTests+ICommandWithPocoResult':
                //   result types 'IMoreResult', 'IAnotherResult' must resolve to a common most specific type.
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( ICommandUnifiedWithTheResult ), typeof( IUnifiedResult ) );
                var d = TestHelper.GetAutomaticServices( c ).Services.GetRequiredService<CommandDirectory>();
                var cmdModel = d.Commands[0];
                cmdModel.CommandType.Should().BeAssignableTo( typeof( ICommandUnifiedWithTheResult ) );
                cmdModel.ResultType.Should().BeAssignableTo( typeof( IUnifiedResult ) );
            }
        }



    }
}
