using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using static CK.Cris.Tests.ICommandHandlerTests;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;
using System;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.Tests
{

    [TestFixture]
    public class CrisDirectoryTests
    {
        [ExternalName( "Test", "PreviousTest1", "PreviousTest2" )]
        public interface ITestCommand : ICommand
        {
        }

        [Test]
        public void simple_command_models()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), typeof( ITestCommand ) );
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

        public interface ITestSpecCommand : ITestCommand, IEvent
        {
        }

        [Test]
        public void IEvent_cannot_be_a_ICommand()
        {
            using( TestHelper.Monitor.CollectTexts( out var texts ) )
            {
                var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), typeof( ITestSpecCommand ) );
                TestHelper.GenerateCode( c, null ).Success.Should().BeFalse();
                texts.Should().Contain( "Cris '[PrimaryPoco]CK.Cris.Tests.CrisDirectoryTests.ITestCommand' cannot be both a IEvent and a IAbstractCommand." );
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

        public interface ICultureCommand : ICommandWithCurrentCulture
        {
        }

        // Required to test ConfigureAmbientService since for a non handled commands
        // validators, configurators and post handlers are trimmed out.
        public sealed class FakeCommandHandler : IAutoService
        {
            [CommandHandler]
            public void Handle( ICultureCommand command ) { }
        }

        [Test]
        public void configuring_AmbientServiceHub()
        {
            NormalizedCultureInfo.EnsureNormalizedCultureInfo( "fr" );

            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ),
                                                     typeof( CrisCultureService ),
                                                     typeof( NormalizedCultureInfo ),
                                                     typeof( NormalizedCultureInfoUbiquitousServiceDefault ),
                                                     typeof( ICultureCommand ),
                                                     typeof( FakeCommandHandler ) );
            using var services = TestHelper.CreateAutomaticServices( c ).Services;
            services.GetRequiredService<IEnumerable<IHostedService>>().Should().HaveCount( 1, "Required to initialize the Global Service Provider." );

            using( var scope = services.CreateScope() )
            {
                var s = scope.ServiceProvider;

                var poco = s.GetRequiredService<PocoDirectory>();
                var cmd = poco.Create<ICultureCommand>( c => c.CurrentCultureName = "fr" );

                var ambient = s.GetRequiredService<AmbientServiceHub>();
                ambient.GetCurrentValue<ExtendedCultureInfo>().Name.Should().Be( "en" );
                ambient.IsLocked.Should().BeTrue();

                FluentActions.Invoking( () => cmd.CrisPocoModel.ConfigureAmbientServices( cmd, ambient ) )
                    .Should().Throw<InvalidOperationException>();

                ambient = ambient.CleanClone();
                cmd.CrisPocoModel.ConfigureAmbientServices( cmd, ambient );

                ambient.GetCurrentValue<ExtendedCultureInfo>().Name.Should().Be( "fr" );
            }
        }

    }
}

