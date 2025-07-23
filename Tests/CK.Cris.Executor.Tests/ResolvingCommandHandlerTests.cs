using CK.Auth;
using CK.Core;
using CK.Cris.Executor.Tests;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.Tests;

public interface ISomeResult : IStandardResultPart
{
}

public interface IGetSomethingQCommand : ICommand<ISomeResult>
{
}

public interface ISpecialized1Result : ISomeResult
{
}

public interface ISpecialized2Result : ISomeResult
{
}

public sealed class DefaultImpl : IRealObject
{
    [CommandHandler( AllowUnclosedCommand = true )]
    public ISomeResult GetSomeResult( UserMessageCollector userMessage, IGetSomethingQCommand cmd )
    {
        userMessage.Info( "From DefaultImpl" );
        return cmd.CreateResult( r => r.SetUserMessages( userMessage ) );
    }
}

// The GetSomeResult handler competes with the DefaultImpl one but its returned type is better.
public sealed class Specialized1Impl : IRealObject
{
    [AllowNull] PocoDirectory _pocoDirectory;
    [AllowNull] DefaultImpl _defaultImpl;

    void StObjConstruct( DefaultImpl defaultImpl, PocoDirectory pocoDirectory )
    {
        _defaultImpl = defaultImpl;
        _pocoDirectory = pocoDirectory;
    }

    [CommandHandler]
    public ISpecialized1Result GetSomeResult( UserMessageCollector userMessage, IGetSomethingQCommand cmd )
    {
        userMessage.Info( "From SpecializedImpl n°1" );
        return _pocoDirectory.Create<ISpecialized1Result>( r => r.SetUserMessages( userMessage ) );
    }
}

//
// The GetSomeResult handler competes with the DefaultImpl and Specialized1Impl and its returned
// type is better another secondary poco type: it will conflict with the Specialized1Impl.
//
public sealed class Specialized2IndependentImpl : IRealObject
{
    [AllowNull] PocoDirectory _pocoDirectory;
    [AllowNull] DefaultImpl _defaultImpl;

    void StObjConstruct( DefaultImpl defaultImpl, PocoDirectory pocoDirectory )
    {
        _defaultImpl = defaultImpl;
        _pocoDirectory = pocoDirectory;
    }

    [CommandHandler]
    public ISpecialized2Result GetSomeResult( UserMessageCollector userMessage, IGetSomethingQCommand cmd )
    {
        userMessage.Info( "From Specialized2IndependentImpl n°2" );
        return _pocoDirectory.Create<ISpecialized2Result>( r => r.SetUserMessages( userMessage ) );
    }
}

[TestFixture]
public class ResolvingCommandHandlerTests
{
    [Test]
    public async Task more_specialized_returned_type_disambiguate_handlers_Async()
    {
        using var _ = TestHelper.Monitor.CollectEntries( out var logs, LogLevelFilter.Info );
        await using var auto = await TestHelper.CreateAutomaticServicesWithMonitorAsync(
        [
            typeof( CrisExecutionContext ),
            typeof( UserMessageCollector ),
            typeof( CurrentCultureInfo ),
            typeof( NormalizedCultureInfo ),
            typeof( TranslationService ),
            typeof( NormalizedCultureInfoAmbientServiceDefault ),
            typeof( ISpecialized1Result ),
            typeof( IGetSomethingQCommand ),
            typeof( DefaultImpl ),
            typeof( Specialized1Impl )
        ] );

        logs.Where( e => e.MaskedLevel == LogLevel.Info )
            .Select( e => e.Text )
            .ShouldContain( "Handler method 'CK.Cris.Tests.DefaultImpl.GetSomeResult' is skipped since " +
                            "'Specialized1Impl.GetSomeResult( UserMessageCollector userMessage, IGetSomethingQCommand cmd )' " +
                            "returns a more specialized result for 'CK.Cris.Tests.IGetSomethingQCommand' command." );

        using( var scope = auto.Services.CreateScope() )
        {
            var services = scope.ServiceProvider;

            var pocoDirectory = services.GetRequiredService<PocoDirectory>();
            var executionContext = services.GetRequiredService<CrisExecutionContext>();

            var cmd = pocoDirectory.Create<IGetSomethingQCommand>();
            var result = await executionContext.ExecuteRootCommandAsync( cmd );
            result.Command.ShouldBeSameAs( cmd );
            result.Result.ShouldNotBeNull()
                         .ShouldBeAssignableTo<ISpecialized1Result>()
                         .UserMessages
                            .ShouldHaveSingleItem()
                            .Text.ShouldBe( "From SpecializedImpl n°1" );
        }
    }

    [Test]
    public async Task unrelated_return_types_are_ambiguous_handlers_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add(
            typeof( CrisExecutionContext ),
            typeof( UserMessageCollector ),
            typeof( CurrentCultureInfo ),
            typeof( NormalizedCultureInfo ),
            typeof( TranslationService ),
            typeof( NormalizedCultureInfoAmbientServiceDefault ),
            typeof( ISpecialized1Result ),
            typeof( ISpecialized2Result ),
            typeof( IGetSomethingQCommand ),
            typeof( DefaultImpl ),
            typeof( Specialized1Impl ),
            typeof( Specialized2IndependentImpl ) );

        await configuration.GetFailedAutomaticServicesAsync( """
            Ambiguity: cannot choose between the following handlers for 'CK.Cris.Tests.IGetSomethingQCommand' command as they return unrelated types:
            Specialized2IndependentImpl.GetSomeResult( UserMessageCollector userMessage, IGetSomethingQCommand cmd ) returns 'CK.Cris.Tests.ISpecialized2Result?'
            and
            Specialized1Impl.GetSomeResult( UserMessageCollector userMessage, IGetSomethingQCommand cmd ) returns 'CK.Cris.Tests.ISpecialized1Result?'
            """ );
    }

    // Better than IGetSomethingQCommand: BetterImpl handle this type.
    public interface IGetSomethingMoreQCommand : IGetSomethingQCommand
    {
    }

    // Also better than IGetSomethingQCommand but unused in the following test:
    // this prevents IGetSomethingMoreQCommand to be the closure: DefaultImpl
    // and BetterImpl compete but BetterImpl is better.
    public interface IGetSomethingOtherMoreQCommand : IGetSomethingQCommand
    {
    }

    public sealed class BetterImpl : IRealObject
    {
        [CommandHandler( AllowUnclosedCommand = true )]
        public ISomeResult GetSomeResult( UserMessageCollector userMessage, IGetSomethingMoreQCommand cmd )
        {
            userMessage.Info( "From BetterImpl" );
            return cmd.CreateResult( r => r.SetUserMessages( userMessage ) );
        }
    }

    [Test]
    public async Task more_specialized_command_type_disambiguate_handlers_Async()
    {
        using var _ = TestHelper.Monitor.CollectEntries( out var logs, LogLevelFilter.Info );
        await using var auto = await TestHelper.CreateAutomaticServicesWithMonitorAsync(
        [
            typeof( CrisExecutionContext ),
            typeof( UserMessageCollector ),
            typeof( CurrentCultureInfo ),
            typeof( NormalizedCultureInfo ),
            typeof( TranslationService ),
            typeof( NormalizedCultureInfoAmbientServiceDefault ),
            typeof( ISomeResult ),
            typeof( IGetSomethingMoreQCommand ),
            typeof( IGetSomethingOtherMoreQCommand ),
            typeof( DefaultImpl ),
            typeof( BetterImpl )
        ] );

        logs.Where( e => e.MaskedLevel == LogLevel.Info )
            .Select( e => e.Text )
            .ShouldContain( "Handler method 'CK.Cris.Tests.DefaultImpl.GetSomeResult' is skipped since 'BetterImpl.GetSomeResult( UserMessageCollector userMessage, IGetSomethingMoreQCommand cmd )' handles a specialized 'CK.Cris.Tests.IGetSomethingQCommand' command." );

        using( var scope = auto.Services.CreateScope() )
        {
            var services = scope.ServiceProvider;

            var pocoDirectory = services.GetRequiredService<PocoDirectory>();
            var executionContext = services.GetRequiredService<CrisExecutionContext>();

            var cmd = pocoDirectory.Create<IGetSomethingQCommand>();
            var result = await executionContext.ExecuteRootCommandAsync( cmd );
            result.Command.ShouldBeSameAs( cmd );
            result.Result.ShouldNotBeNull()
                         .ShouldBeAssignableTo<ISomeResult>()
                         .UserMessages
                            .ShouldHaveSingleItem()
                            .Text.ShouldBe( "From BetterImpl" );
        }
    }

}
