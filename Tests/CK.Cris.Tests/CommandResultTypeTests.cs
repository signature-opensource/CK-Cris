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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

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
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), typeof( ICInt ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;

            var d = s.GetRequiredService<CrisDirectory>();
            d.CrisPocoModels[0].ResultType.Should().Be( typeof( int ) );
        }

        public interface ICIntButObject : ICInt, ICommand<object>
        {
        }

        [Test]
        public void a_specialized_command_can_generalize_the_initial_result_type()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), typeof( ICIntButObject ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<CrisDirectory>();
            var cmdModel = d.CrisPocoModels[0];
            cmdModel.CommandType.Should().BeAssignableTo( typeof( ICIntButObject ) );
            cmdModel.ResultType.Should().Be( typeof( int ) );
        }

        public interface ICIntButString : ICInt, ICommand<string>
        {
        }

        [Test]
        public void incompatible_result_type()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), typeof( ICIntButString ) );
            TestHelper.GenerateCode( c, engineConfigurator: null ).Success.Should().BeFalse();
            //=> Invalid command Result type for 'CK.Cris.Tests.CommandResultTypeTests+ICInt': result types 'Int32', 'String' must resolve to a common most specific type.
        }

        [Test]
        public void when_IPoco_result_are_not_closed_this_is_invalid()
        {
            {
                var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), typeof( IWithMorePocoResultCommand ), typeof( IMoreResult ) );
                using var s = TestHelper.CreateAutomaticServices( c ).Services;
                var d = s.GetRequiredService<CrisDirectory>();
                var cmdModel = d.CrisPocoModels[0];
                cmdModel.CommandType.Should().BeAssignableTo( typeof( IWithMorePocoResultCommand ) );
                cmdModel.ResultType.Should().BeAssignableTo( typeof( IMoreResult ) );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), typeof( IWithAnotherPocoResultCommand ), typeof( IAnotherResult ) );
                using var s = TestHelper.CreateAutomaticServices( c ).Services;
                var d = s.GetRequiredService<CrisDirectory>();
                var cmdModel = d.CrisPocoModels[0];
                cmdModel.CommandType.Should().BeAssignableTo( typeof( IWithAnotherPocoResultCommand ) );
                cmdModel.ResultType.Should().BeAssignableTo( typeof( IAnotherResult ) );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), typeof( IUnifiedButNotTheResultCommand ), typeof( IMoreResult ), typeof( IAnotherResult ) );
                TestHelper.GenerateCode( c, null ).Success.Should().BeFalse();
                //=> Invalid command Result type for 'CK.Cris.Tests.CommandResultTypeTests+ICommandWithPocoResult':
                //   result types 'IMoreResult', 'IAnotherResult' must resolve to a common most specific type.
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), typeof( IWithTheResultUnifiedCommand ), typeof( IUnifiedResult ) );
                using var s = TestHelper.CreateAutomaticServices( c ).Services;
                var d = s.GetRequiredService<CrisDirectory>();
                var cmdModel = d.CrisPocoModels[0];
                cmdModel.CommandType.Should().BeAssignableTo( typeof( IWithTheResultUnifiedCommand ) );
                cmdModel.ResultType.Should().BeAssignableTo( typeof( IUnifiedResult ) );
            }
        }



    }
}
