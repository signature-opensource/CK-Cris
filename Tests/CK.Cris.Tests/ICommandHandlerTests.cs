using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.Tests;

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
    public async Task Command_method_handler_must_exist_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ),
                                              typeof( IWithTheResultUnifiedCommand ),
                                              typeof( IUnifiedResult ),
                                              typeof( CmdHandlerMissingHandler ) );
        await configuration.GetFailedAutomaticServicesAsync(
            "Service 'CK.Cris.Tests.ICommandHandlerTests.CmdHandlerMissingHandler' must implement a command handler method for closed command CK.Cris.Tests.IWithPocoResultCommand of the closing type CK.Cris.Tests.IWithTheResultUnifiedCommand." );
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
        public IResult Run( IWithPocoResultCommand r, IPocoFactory<IResult> resultFacory ) => throw new Exception( "Hidden." );
    }

    [Test]
    public async Task ICommandHandler_has_the_priority_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ),
                                              typeof( IWithPocoResultCommand ),
                                              typeof( IResult ),
                                              typeof( CmdHandlerOfBase ),
                                              typeof( CmdHandlerAlternate ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var d = auto.Services.GetRequiredService<CrisDirectory>();

        var cmd = auto.Services.GetRequiredService<IPocoFactory<IWithPocoResultCommand>>().Create();
        var handler = cmd.CrisPocoModel.Handlers.Single();
        handler.Type.FinalType.ShouldBeSameAs( typeof( CmdHandlerOfBase ) );
    }

    public class CmdHandlerWithMore : CmdHandlerOfBase
    {
        [CommandHandler]
        public IMoreResult RunMore( IPocoFactory<IMoreResult> resultFacory, IWithMorePocoResultCommand r ) => resultFacory.Create();

    }

    [Test]
    public async Task ICommandHandler_implementation_can_be_specialized_without_redefining_command_type_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ),
                                              typeof( IWithMorePocoResultCommand ),
                                              typeof( IMoreResult ),
                                              typeof( CmdHandlerWithMore ),
                                              typeof( CmdHandlerAlternate ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var d = auto.Services.GetRequiredService<CrisDirectory>();

        var cmd = auto.Services.GetRequiredService<IPocoFactory<IWithPocoResultCommand>>().Create();
        cmd.ShouldBeAssignableTo<IWithMorePocoResultCommand>();

        var handler = cmd.CrisPocoModel.Handlers.Single();
        handler.Type.FinalType.ShouldBeSameAs( typeof( CmdHandlerWithMore ) );
    }

    public class CmdHandlerWithAnother : CmdHandlerOfBase
    {
        [CommandHandler]
        public IAnotherResult RunAnother( IPocoFactory<IAnotherResult> resultFacory, IWithAnotherPocoResultCommand r ) => resultFacory.Create();
    }

    [Test]
    public void Command_handler_service_must_be_unified_just_like_other_IAutoService()
    {
        TestHelper.GetFailedCollectorResult(
            [typeof( CrisDirectory ),
                typeof( IWithTheResultUnifiedCommand ),
                typeof( CmdHandlerWithMore ),
                typeof( CmdHandlerWithAnother ),
                typeof( CmdHandlerAlternate )
            ],
            "Service Class Unification: unable to resolve 'CK.Cris.Tests.ICommandHandlerTests+CmdHandlerOfBase' to a unique specialization.",
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
    public async Task Command_method_handler_must_also_be_unified_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ),
                                              typeof( IWithTheResultUnifiedCommand ),
                                              typeof( CmdHandlerFailingUnified ),
                                              typeof( CmdHandlerWithMore ),
                                              typeof( CmdHandlerWithAnother ),
                                              typeof( CmdHandlerAlternate ),
                                              typeof( IUnifiedResult ),
                                              typeof( IMoreResult ),
                                              typeof( IAnotherResult ),
                                              typeof( IResult ) );
        await configuration.GetFailedAutomaticServicesAsync(
            "Service 'CK.Cris.Tests.ICommandHandlerTests.CmdHandlerFailingUnified' must implement a command handler method for closed command CK.Cris.Tests.IWithPocoResultCommand of the closing type CK.Cris.Tests.IWithTheResultUnifiedCommand." );
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
    public async Task ICommandHandler_Service_AND_handler_method_must_be_unified_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ),
                                              typeof( IWithTheResultUnifiedCommand ),
                                              typeof( IUnifiedResult ),
                                              typeof( CmdHandlerUnified ),
                                              typeof( CmdHandlerWithMore ),
                                              typeof( CmdHandlerWithAnother ),
                                              typeof( CmdHandlerAlternate ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var d = auto.Services.GetRequiredService<CrisDirectory>();

        var cmd = auto.Services.GetRequiredService<IPocoFactory<IWithPocoResultCommand>>().Create();
        cmd.ShouldBeAssignableTo<IWithTheResultUnifiedCommand>();

        var handler = cmd.CrisPocoModel.Handlers.Single();
        handler.Type.FinalType.ShouldBeSameAs( typeof( CmdHandlerUnified ) );
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
            result.Val.ShouldBe( 0 );
            result.Val = 3712;
            return result;
        }
    }

    [Test]
    public async Task Handler_method_can_be_virtual_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ),
                                              typeof( IWithTheResultUnifiedCommand ),
                                              typeof( IUnifiedResult ),
                                              typeof( CmdHandlerUnifiedSpecialized ), typeof( CmdHandlerWithMore ), typeof( CmdHandlerWithAnother ), typeof( CmdHandlerAlternate ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var d = auto.Services.GetRequiredService<CrisDirectory>();

        var cmd = auto.Services.GetRequiredService<IPocoFactory<IWithPocoResultCommand>>().Create();
        cmd.ShouldBeAssignableTo<IWithTheResultUnifiedCommand>();

        var model = cmd.CrisPocoModel;
        model.Handlers.ShouldNotBeEmpty();
        var handler = model.Handlers.Single();

        var handlerService = auto.Services.GetRequiredService<ICommandHandler<IWithPocoResultCommand>>();
        var method = handlerService.GetType().GetMethod( handler.MethodName, handler.Parameters.ToArray() );
        Throw.DebugAssert( method != null );
        var result = (IResult?)method.Invoke( handlerService, new[] { cmd } );
        Throw.DebugAssert( result != null );
        result.Val.ShouldBe( 3712, "Calling the base method naturally uses the overridden method." );
    }

    public interface ITestCommand : ICommand<int>
    {
        string Text { get; set; }
    }

    // For members attributes to kick in the code generation process,
    // at least one IAttributeContextBound must exist on the type itself.
    public class BaseClassWithHandler
    {
        [CommandHandler]
        public int Run( ITestCommand cmd ) => cmd.Text.Length;
    }

    [Test]
    public async Task Handler_method_on_regular_class_is_ignored_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ),
                                              typeof( ITestCommand ),
                                              typeof( BaseClassWithHandler ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var d = auto.Services.GetRequiredService<CrisDirectory>();
        d.CrisPocoModels.Count.ShouldBe( 1 );
        d.CrisPocoModels[0].Handlers.ShouldBeEmpty();
    }

    public abstract class SpecializedBaseClassService : BaseClassWithHandler, IAutoService
    {
        [CommandHandler]
        public new int Run( ITestCommand cmd ) => base.Run( cmd );
    }

    [Test]
    public async Task Handler_method_can_be_relayed_to_base_class_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ),
                                              typeof( ITestCommand ),
                                              typeof( SpecializedBaseClassService ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var d = auto.Services.GetRequiredService<CrisDirectory>();
        d.CrisPocoModels.Count.ShouldBe( 1 );
        var handler = d.CrisPocoModels[0].Handlers.Single();
        Throw.DebugAssert( handler != null );
        handler.Type.ClassType.ShouldBe( typeof( SpecializedBaseClassService ) );
        handler.Type.FinalType.FullName.ShouldBe( "CK.Cris.Tests.ICommandHandlerTests_SpecializedBaseClassService_CK" );
        handler.Type.IsScoped.ShouldBe( false );
        handler.Type.MultipleMappings.ShouldBeEmpty();
        handler.Type.UniqueMappings.ShouldBeEmpty();
    }

}
