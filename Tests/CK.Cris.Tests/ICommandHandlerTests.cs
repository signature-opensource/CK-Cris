using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.Tests
{
    [TestFixture]
    public class ICommandHandlerTests
    {
        public class CmdHandlerMissingHandler : ICommandHandler<ICommandUnifiedWithTheResult>
        {
        }

        [Test]
        public void Command_method_handler_must_exist()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( AmbientValues.IAmbientValues ),
                typeof( ICommandUnifiedWithTheResult ),
                typeof( IUnifiedResult),
                typeof( CmdHandlerMissingHandler ) );
            TestHelper.GenerateCode( c, null ).Success.Should().BeFalse();
        }

        public class CmdHandlerOfBase : ICommandHandler<ICommandWithPocoResult>
        {
            [CommandHandler]
            public IResult Run( IPocoFactory<IResult> resultFacory, ICommandWithPocoResult r ) => resultFacory.Create();
        }

        // This one is not ICommandHandler: it will be skipped in favor of CmdHandlerOfBase.
        public class CmdHandlerAlternate
        {
            [CommandHandler]
            public IResult Run( ICommandWithPocoResult r, IPocoFactory<IResult> resultFacory ) => throw new Exception("Hidden.");
        }

        [Test]
        public void ICommandHandler_has_the_priority()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( AmbientValues.IAmbientValues ),
                typeof( ICommandWithPocoResult ),
                typeof( CmdHandlerOfBase ),
                typeof( CmdHandlerAlternate ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<CommandDirectory>();

            var cmd = s.GetRequiredService<IPocoFactory<ICommandWithPocoResult>>().Create();
            var model = cmd.CommandModel;
            model.Handler.Should().NotBeNull().And.BeSameAs( typeof( CmdHandlerOfBase ).GetMethod( "Run" ) );
        }

        public class CmdHandlerWithMore : CmdHandlerOfBase
        {
            [CommandHandler]
            public IMoreResult RunMore( IPocoFactory<IMoreResult> resultFacory, ICommandWithMorePocoResult r ) => resultFacory.Create();

        }

        [Test]
        public void ICommandHandler_implementation_can_be_specialized_without_redefining_command_type()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( AmbientValues.IAmbientValues ),
                typeof( ICommandWithMorePocoResult ),
                typeof( CmdHandlerWithMore ),
                typeof( CmdHandlerAlternate ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<CommandDirectory>();

            var cmd = s.GetRequiredService<IPocoFactory<ICommandWithPocoResult>>().Create();
            cmd.Should().BeAssignableTo<ICommandWithMorePocoResult>();

            var model = cmd.CommandModel;
            model.Handler.Should().NotBeNull().And.BeSameAs( typeof( CmdHandlerWithMore ).GetMethod( "RunMore" ) );
        }

        public class CmdHandlerWithAnother : CmdHandlerOfBase
        {
            [CommandHandler]
            public IAnotherResult RunAnother( IPocoFactory<IAnotherResult> resultFacory, ICommandWithAnotherPocoResult r ) => resultFacory.Create();
        }

        [Test]
        public void Command_handler_service_must_be_unified_just_like_other_IAutoService()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( AmbientValues.IAmbientValues ),
                typeof( ICommandUnifiedWithTheResult ),
                typeof( CmdHandlerWithMore ),
                typeof( CmdHandlerWithAnother ),
                typeof( CmdHandlerAlternate ) );
            TestHelper.GetFailedResult( c );
        }

        // This service unifies the Service, but doesn't solve the ICommand and IResult diamond: this fails (not during the service analysis like above
        // but later during the Cris command analysis.
        public class CmdHandlerFailingUnified : CmdHandlerWithMore
        {
            public CmdHandlerFailingUnified( CmdHandlerWithAnother handled )
            {
            }
        }

        [Test]
        public void Command_method_handler_must_also_be_unified()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( AmbientValues.IAmbientValues ),
                typeof( ICommandUnifiedWithTheResult ),
                typeof( CmdHandlerFailingUnified ),
                typeof( CmdHandlerWithMore ),
                typeof( CmdHandlerWithAnother ),
                typeof( CmdHandlerAlternate ) );
            TestHelper.GenerateCode( c, null ).Success.Should().BeFalse();
        }

        // This one unifies the Services AND offer a final handler.
        public class CmdHandlerUnified : CmdHandlerWithMore
        {
            readonly IPocoFactory<IUnifiedResult> _resultFacory;

            public CmdHandlerUnified( CmdHandlerWithAnother handled, IPocoFactory<IUnifiedResult> resultFacory )
            {
                _resultFacory = resultFacory;
            }

            [CommandHandler]
            public virtual IUnifiedResult Run( ICommandUnifiedWithTheResult r ) => _resultFacory.Create();
        }

        [Test]
        public void ICommandHandler_Service_AND_handler_method_must_be_unified()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( AmbientValues.IAmbientValues ),
                typeof( ICommandUnifiedWithTheResult ),
                typeof( CmdHandlerUnified ),
                typeof( CmdHandlerWithMore ),
                typeof( CmdHandlerWithAnother ),
                typeof( CmdHandlerAlternate ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<CommandDirectory>();

            var cmd = s.GetRequiredService<IPocoFactory<ICommandWithPocoResult>>().Create();
            cmd.Should().BeAssignableTo<ICommandUnifiedWithTheResult>();

            var model = cmd.CommandModel;
            model.Handler.Should().NotBeNull().And.BeSameAs( typeof( CmdHandlerUnified ).GetMethod( "Run", new[] { typeof( ICommandUnifiedWithTheResult ) } ) );
        }

        // Handlers can be virtual.
        public class CmdHandlerUnifiedSpecialized : CmdHandlerUnified
        {
            public CmdHandlerUnifiedSpecialized( CmdHandlerWithAnother handled, IPocoFactory<IUnifiedResult> resultFacory )
                : base( handled, resultFacory )
            {
            }

            public override IUnifiedResult Run( ICommandUnifiedWithTheResult r )
            {
                var result = base.Run( r );
                result.Val.Should().Be( 0 );
                result.Val = 3712;
                return result;
            }
        }

        [Test]
        public void Handler_method_can_be_virtual()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( AmbientValues.IAmbientValues ),
                                                     typeof( ICommandUnifiedWithTheResult ),
                                                     typeof( IUnifiedResult ),
                                                     typeof( CmdHandlerUnifiedSpecialized ), typeof( CmdHandlerWithMore ), typeof( CmdHandlerWithAnother ), typeof( CmdHandlerAlternate ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<CommandDirectory>();

            var cmd = s.GetRequiredService<IPocoFactory<ICommandWithPocoResult>>().Create();
            cmd.Should().BeAssignableTo<ICommandUnifiedWithTheResult>();

            var baseHandler = typeof( CmdHandlerUnified ).GetMethod( "Run", new[] { typeof( ICommandUnifiedWithTheResult ) } );

            var model = cmd.CommandModel;
            Debug.Assert( model.Handler != null );
            model.Handler.Should().NotBeNull().And.BeSameAs( baseHandler, "The base method is the one decorated by the [CommandHandler]." );

            var handlerService = s.GetRequiredService<ICommandHandler<ICommandWithPocoResult>>();

            var result = (IResult?)model.Handler.Invoke( handlerService, new[] { cmd } );
            Debug.Assert( result != null );
            result.Val.Should().Be( 3712, "Calling the base method naturally uses the overridden method." );
        }

        public interface ICmdTest : ICommand<int>
        {
            string Text { get; set; }
        }

        public class BaseClassWithHandler
        {
            [CommandHandler]
            public int Run( ICmdTest cmd ) => cmd.Text.Length;
        }

        [Test]
        public void Handler_method_on_regular_class_is_NOT_discovered()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( AmbientValues.IAmbientValues ),
                                                     typeof( ICmdTest ),
                                                     typeof( BaseClassWithHandler ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<CommandDirectory>();
            d.Commands.Should().HaveCount( 1 );
            d.Commands[0].Handler.Should().BeNull();
        }

        public class SpecializedBaseClassService : BaseClassWithHandler, IAutoService
        {
        }

        [Test]
        public void Handler_method_of_base_class_is_discovered_by_inheritance()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommandDirectory ), typeof( AmbientValues.IAmbientValues ),
                                                     typeof( ICmdTest ),
                                                     typeof( SpecializedBaseClassService ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<CommandDirectory>();
            d.Commands.Should().HaveCount( 1 );
            d.Commands[0].Handler.Should().NotBeNull();
        }

    }
}
