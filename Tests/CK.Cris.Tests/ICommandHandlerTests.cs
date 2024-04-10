using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.Tests
{
    [TestFixture]
    public class ICommandHandlerTests
    {
        public class CmdHandlerMissingHandler : ICommandHandler<IWithTheResultUnifiedCommand>
        {
        }

        // When ICommandHandler<T> is used, there MUST be a handler method.
        // (Note that this doesn't mean that a Command must have a handler: command can exist
        // without handlers in some deployment.)
        [Test]
        public void Command_method_handler_must_exist()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ),
                                                     typeof( IWithTheResultUnifiedCommand ),
                                                     typeof( IUnifiedResult),
                                                     typeof( CmdHandlerMissingHandler ) );
            TestHelper.GenerateCode( c, null ).Success.Should().BeFalse();
        }

        public class CmdHandlerOfBase : ICommandHandler<IWithPocoResultCommand>
        {
            [CommandHandler]
            public IResult Run( IPocoFactory<IResult> resultFacory, IWithPocoResultCommand r ) => resultFacory.Create();
        }

        // This one is not ICommandHandler: it will be skipped in favor of CmdHandlerOfBase.
        public class CmdHandlerAlternate
        {
            [CommandHandler]
            public IResult Run( IWithPocoResultCommand r, IPocoFactory<IResult> resultFacory ) => throw new Exception("Hidden.");
        }

        [Test]
        public void ICommandHandler_has_the_priority()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ),
                                                     typeof( IWithPocoResultCommand ),
                                                     typeof( IResult ),
                                                     typeof( CmdHandlerOfBase ),
                                                     typeof( CmdHandlerAlternate ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<CrisDirectory>();

            var cmd = s.GetRequiredService<IPocoFactory<IWithPocoResultCommand>>().Create();
            var handler = cmd.CrisPocoModel.Handlers.Single();
            handler.Type.FinalType.Should().NotBeNull().And.BeSameAs( typeof( CmdHandlerOfBase ) );
        }

        public class CmdHandlerWithMore : CmdHandlerOfBase
        {
            [CommandHandler]
            public IMoreResult RunMore( IPocoFactory<IMoreResult> resultFacory, IWithMorePocoResultCommand r ) => resultFacory.Create();

        }

        [Test]
        public void ICommandHandler_implementation_can_be_specialized_without_redefining_command_type()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ),
                                                     typeof( IWithMorePocoResultCommand ),
                                                     typeof( IMoreResult ),
                                                     typeof( CmdHandlerWithMore ),
                                                     typeof( CmdHandlerAlternate ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<CrisDirectory>();

            var cmd = s.GetRequiredService<IPocoFactory<IWithPocoResultCommand>>().Create();
            cmd.Should().BeAssignableTo<IWithMorePocoResultCommand>();

            var handler = cmd.CrisPocoModel.Handlers.Single();
            handler.Type.FinalType.Should().NotBeNull().And.BeSameAs( typeof( CmdHandlerWithMore ) );
        }

        public class CmdHandlerWithAnother : CmdHandlerOfBase
        {
            [CommandHandler]
            public IAnotherResult RunAnother( IPocoFactory<IAnotherResult> resultFacory, IWithAnotherPocoResultCommand r ) => resultFacory.Create();
        }

        [Test]
        public void Command_handler_service_must_be_unified_just_like_other_IAutoService()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ),
                                                     typeof( IWithTheResultUnifiedCommand ),
                                                     typeof( CmdHandlerWithMore ),
                                                     typeof( CmdHandlerWithAnother ),
                                                     typeof( CmdHandlerAlternate ) );
            TestHelper.GetFailedResult( c, "Service Class Unification: unable to resolve 'CK.Cris.Tests.ICommandHandlerTests+CmdHandlerOfBase' to a unique specialization.",
                                           "Base class 'CK.Cris.Tests.ICommandHandlerTests+CmdHandlerOfBase' cannot be unified by any of this candidates: 'ICommandHandlerTests.CmdHandlerWithAnother', 'ICommandHandlerTests.CmdHandlerWithMore'." );
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
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ),
                typeof( IWithTheResultUnifiedCommand ),
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
            public virtual IUnifiedResult Run( IWithTheResultUnifiedCommand r ) => _resultFacory.Create();
        }

        [Test]
        public void ICommandHandler_Service_AND_handler_method_must_be_unified()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ),
                                                     typeof( IWithTheResultUnifiedCommand ),
                                                     typeof( IUnifiedResult ),
                                                     typeof( CmdHandlerUnified ),
                                                     typeof( CmdHandlerWithMore ),
                                                     typeof( CmdHandlerWithAnother ),
                                                     typeof( CmdHandlerAlternate ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<CrisDirectory>();

            var cmd = s.GetRequiredService<IPocoFactory<IWithPocoResultCommand>>().Create();
            cmd.Should().BeAssignableTo<IWithTheResultUnifiedCommand>();

            var handler = cmd.CrisPocoModel.Handlers.Single();
            handler.Type.FinalType.Should().NotBeNull().And.BeSameAs( typeof( CmdHandlerUnified ) );
        }

        // Handlers can be virtual.
        public class CmdHandlerUnifiedSpecialized : CmdHandlerUnified
        {
            public CmdHandlerUnifiedSpecialized( CmdHandlerWithAnother handled, IPocoFactory<IUnifiedResult> resultFacory )
                : base( handled, resultFacory )
            {
            }

            public override IUnifiedResult Run( IWithTheResultUnifiedCommand r )
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
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ),
                                                     typeof( IWithTheResultUnifiedCommand ),
                                                     typeof( IUnifiedResult ),
                                                     typeof( CmdHandlerUnifiedSpecialized ), typeof( CmdHandlerWithMore ), typeof( CmdHandlerWithAnother ), typeof( CmdHandlerAlternate ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<CrisDirectory>();

            var cmd = s.GetRequiredService<IPocoFactory<IWithPocoResultCommand>>().Create();
            cmd.Should().BeAssignableTo<IWithTheResultUnifiedCommand>();

            var model = cmd.CrisPocoModel;
            model.Handlers.Should().NotBeEmpty();
            var handler = model.Handlers.Single();

            var handlerService = s.GetRequiredService<ICommandHandler<IWithPocoResultCommand>>();
            var method = handlerService.GetType().GetMethod( handler.MethodName, handler.Parameters.ToArray() );
            Throw.DebugAssert( method != null );
            var result = (IResult?)method.Invoke( handlerService, new[] { cmd } );
            Throw.DebugAssert( result != null );
            result.Val.Should().Be( 3712, "Calling the base method naturally uses the overridden method." );
        }

        public interface ICmdTest : ICommand<int>
        {
            string Text { get; set; }
        }

        // For members attributes to kick in the code generation process,
        // at least one IAttributeContextBound must exist on the type itself.
        public class BaseClassWithHandler
        {
            [CommandHandler]
            public int Run( ICmdTest cmd ) => cmd.Text.Length;
        }

        [Test]
        public void Handler_method_on_regular_class_is_NOT_discovered()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ),
                                                     typeof( ICmdTest ),
                                                     typeof( BaseClassWithHandler ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<CrisDirectory>();
            d.CrisPocoModels.Should().HaveCount( 1 );
            d.CrisPocoModels[0].Handlers.Should().BeEmpty();
        }

        public abstract class SpecializedBaseClassService : BaseClassWithHandler, IAutoService
        {
        }

        [Test]
        public void Handler_method_of_base_class_is_discovered_by_inheritance()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), typeof( AmbientValues.IAmbientValues ),
                                                     typeof( ICmdTest ),
                                                     typeof( SpecializedBaseClassService ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<CrisDirectory>();
            d.CrisPocoModels.Should().HaveCount( 1 );
            var handler = d.CrisPocoModels[0].Handlers.Single();
            Throw.DebugAssert( handler != null );
            handler.Type.ClassType.Should().Be( typeof( SpecializedBaseClassService ) );
            handler.Type.FinalType.FullName.Should().Be( "CK.Cris.Tests.ICommandHandlerTests_SpecializedBaseClassService_CK" );
            handler.Type.IsScoped.Should().Be( false );
            handler.Type.MultipleMappings.Should().BeEmpty();
            handler.Type.UniqueMappings.Should().BeEmpty();
        }

    }
}
