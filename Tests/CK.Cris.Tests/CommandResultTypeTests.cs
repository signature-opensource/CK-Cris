using CK.Core;
using CK.Testing;
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
        public interface IIntCommand : ICommand<int>
        {
        }

        [Test]
        public void simple_basic_return()
        {
            var c = TestHelper.CreateTypeCollector( typeof( CrisDirectory ), typeof( IIntCommand ) );
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );

            var d = auto.Services.GetRequiredService<CrisDirectory>();
            d.CrisPocoModels[0].ResultType.Should().Be( typeof( int ) );
        }

        public interface IIntButObjectCommand : IIntCommand, ICommand<object>
        {
        }

        [Test]
        public void a_specialized_command_can_generalize_the_initial_result_type()
        {
            var c = TestHelper.CreateTypeCollector( typeof( CrisDirectory ), typeof( IIntButObjectCommand ) );
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );
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
            var c = TestHelper.CreateTypeCollector( typeof( CrisDirectory ), typeof( IIntButStringCommand ) );
            TestHelper.GetFailedSingleBinPathAutomaticServices( c,
                "Invalid command Result type for 'CK.Cris.Tests.CommandResultTypeTests+IIntCommand': result types 'Int32', 'String' must resolve to a common most specific type." );
        }

        [Test]
        public void when_IPoco_result_are_not_closed_this_is_invalid()
        {
            {
                var c = TestHelper.CreateTypeCollector( typeof( CrisDirectory ), typeof( IWithMorePocoResultCommand ), typeof( IMoreResult ) );
                using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );
                var d = auto.Services.GetRequiredService<CrisDirectory>();
                var cmdModel = d.CrisPocoModels[0];
                cmdModel.CommandType.Should().BeAssignableTo( typeof( IWithMorePocoResultCommand ) );
                cmdModel.ResultType.Should().BeAssignableTo( typeof( IMoreResult ) );
            }
            {
                var c = TestHelper.CreateTypeCollector( typeof( CrisDirectory ), typeof( IWithAnotherPocoResultCommand ), typeof( IAnotherResult ) );
                using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );
                var d = auto.Services.GetRequiredService<CrisDirectory>();
                var cmdModel = d.CrisPocoModels[0];
                cmdModel.CommandType.Should().BeAssignableTo( typeof( IWithAnotherPocoResultCommand ) );
                cmdModel.ResultType.Should().BeAssignableTo( typeof( IAnotherResult ) );
            }
            {
                var c = TestHelper.CreateTypeCollector( typeof( CrisDirectory ), typeof( IUnifiedButNotTheResultCommand ), typeof( IMoreResult ), typeof( IAnotherResult ) );
                TestHelper.GetFailedSingleBinPathAutomaticServices( c,
                    "Invalid command Result type for 'CK.Cris.Tests.CommandResultTypeTests+ICommandWithPocoResult': result types 'IMoreResult', 'IAnotherResult' must resolve to a common most specific type." );
            }
            {
                var c = TestHelper.CreateTypeCollector( typeof( CrisDirectory ), typeof( IWithTheResultUnifiedCommand ), typeof( IUnifiedResult ) );
                using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );
                var d = auto.Services.GetRequiredService<CrisDirectory>();
                var cmdModel = d.CrisPocoModels[0];
                cmdModel.CommandType.Should().BeAssignableTo( typeof( IWithTheResultUnifiedCommand ) );
                cmdModel.ResultType.Should().BeAssignableTo( typeof( IUnifiedResult ) );
            }
        }



    }
}
