using CK.Auth;
using CK.Core;
using FluentAssertions;
using FluentAssertions.Common;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.Executor.Tests
{
    [TestFixture]
    public class RawCrisValidatorTests
    {
        [ExternalName( "Test" )]
        public interface ITestCommand : ICommand
        {
            int Value { get; set; }
        }

        [Test]
        public async Task when_there_is_no_validation_methods_the_validation_succeeds_Async()
        {
            var c = TestHelper.CreateStObjCollector(
                typeof( RawCrisValidator ), typeof( CrisDirectory ), typeof( ICrisResultError ), typeof( AmbientValues.IAmbientValues ),
                typeof( ITestCommand ) );

            using var services = TestHelper.CreateAutomaticServices( c ).Services;
            await TestHelper.StartHostedServicesAsync( services );

            var directory = services.GetRequiredService<PocoDirectory>();
            var cmd = directory.Create<ITestCommand>();

            var validator = services.GetRequiredService<RawCrisValidator>();
            var result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
            result.Success.Should().BeTrue();
        }

        public class BuggyValidator : IAutoService
        {
            [CommandValidator]
            public void ValidateCommand( UserMessageCollector c, ITestCommand cmd )
            {
                throw new Exception( "This should not happen!" );
            }
        }

        [Test]
        public async Task exceptions_raised_by_validators_are_handled_by_the_RawCrisValidator_Async()
        {
            var c = TestHelper.CreateStObjCollector(
                typeof( RawCrisValidator ), typeof( CrisDirectory ), typeof( ICrisResultError ), typeof( AmbientValues.IAmbientValues ),
                typeof( ITestCommand ),
                typeof( BuggyValidator ) );
            using var services = TestHelper.CreateAutomaticServicesWithMonitor( c ).Services;
            await TestHelper.StartHostedServicesAsync( services );

            var directory = services.GetRequiredService<PocoDirectory>();
            var cmd = directory.Create<ITestCommand>();

            var validator = services.GetRequiredService<RawCrisValidator>();
            var result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
            result.Success.Should().BeFalse();
            result.Messages.Should().HaveCount( 1 )
                       .And.Contain( m => m.Level == UserMessageLevel.Error
                                     && m.ResName == "Cris.UnhandledValidationError"
                                     && m.Text.StartsWith( "An unhandled error occurred while validating command 'Test' (LogKey:" ) );
        }

        [ExternalName( "NoValidators" )]
        public interface ICmdWithoutValidators : ICommand
        {
            int AnyValue { get; set; }
        }

        public class SimplestValidatorEverSingleton : IAutoService
        {
            [CommandValidator]
            public void ValidateCommand( UserMessageCollector c, ITestCommand cmd )
            {
                if( cmd.Value < 0 ) c.Error( "[Singleton]Value should be greater than 0." );
                else if( cmd.Value == 0 ) c.Warn( "[Singleton]A positive Value would be better." );
            }
        }

        public class SimplestValidatorEverScoped : IScopedAutoService
        {
            [CommandValidator]
            public void ValidateCommand( UserMessageCollector c, ITestCommand cmd )
            {
                if( cmd.Value < 0 ) c.Error( "[Scoped]Value should be greater than 0." );
                else if( cmd.Value == 0 ) c.Warn( "[Scoped]A positive Value would be better." );
            }
        }

        [TestCase( true, false )]
        [TestCase( false, true )]
        [TestCase( true, true )]
        public async Task the_simplest_validation_is_held_by_a_dependency_free_service_and_is_synchronous_Async( bool scopedService, bool singletonService )
        {
            var c = TestHelper.CreateStObjCollector( typeof( RawCrisValidator ), typeof( CrisDirectory ), typeof( ICrisResultError ), typeof( AmbientValues.IAmbientValues ),
                                                     typeof( ITestCommand ),
                                                     typeof( ICmdWithoutValidators ) );
            if( singletonService ) c.RegisterType( typeof( SimplestValidatorEverSingleton ) );
            if( scopedService ) c.RegisterType( typeof( SimplestValidatorEverScoped ) );

            using var appServices = TestHelper.CreateAutomaticServices( c ).Services;
            await TestHelper.StopHostedServicesAsync( appServices );

            using( var scope = appServices.CreateScope() )
            {
                var services = scope.ServiceProvider;

                var directory = services.GetRequiredService<CrisDirectory>();
                var validator = services.GetRequiredService<RawCrisValidator>();

                var cmd = services.GetRequiredService<IPocoFactory<ITestCommand>>().Create( c => c.Value = -1 );
                var result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeFalse();
                if( scopedService )
                {
                    result.Messages.Should().Contain( m => m.Level == UserMessageLevel.Error && m.Text == "[Scoped]Value should be greater than 0." );
                }
                if( singletonService )
                {
                    result.Messages.Should().Contain( m => m.Level == UserMessageLevel.Error && m.Text == "[Singleton]Value should be greater than 0." );
                }

                cmd.Value = 0;
                result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeTrue();
                result.HasWarnings.Should().BeTrue();
                if( scopedService )
                {
                    result.Messages.Should().Contain( m => m.Level == UserMessageLevel.Warn && m.Text == "[Scoped]A positive Value would be better." );
                }
                if( singletonService )
                {
                    result.Messages.Should().Contain( m => m.Level == UserMessageLevel.Warn && m.Text == "[Singleton]A positive Value would be better." );
                }
                result.Messages.Should().NotContain( m => m.Level == UserMessageLevel.Error );
            }
        }

        public interface IAuthenticatedCommandPart : ICommandPart
        {
            int ActorId { get; set; }
        }

        public class AuthenticationValidator : IAutoService
        {
            [CommandValidator]
            public void ValidateCommand( UserMessageCollector c, IAuthenticatedCommandPart cmd, IAuthenticationInfo info )
            {
                if( cmd.ActorId != info.User.UserId ) c.Error( "Security error." );
            }
        }

        public interface ICmdTestSecure : ITestCommand, IAuthenticatedCommandPart
        {
            bool WarnByAsyncValidator { get; set; }
        }


        public class AsyncValidator : IAutoService
        {
            [CommandValidator]
            public async Task ValidateCommandAsync( UserMessageCollector c, ICmdTestSecure cmd )
            {
                c.Info( "AsyncValidator waiting for result..." );
                await Task.Delay( 20 );
                if( cmd.WarnByAsyncValidator ) c.Warn( "AsyncValidator is not happy!" );
                else c.Info( "AsyncValidator is fine." );
            }
        }

        [Test]
        public async Task part_with_parameter_injection_Async()
        {
            var c = TestHelper.CreateStObjCollector(
                typeof( RawCrisValidator ), typeof( CrisDirectory ), typeof( ICrisResultError ), typeof( AmbientValues.IAmbientValues ),
                typeof( ICmdTestSecure ),
                typeof( AuthenticationValidator ),
                typeof( SimplestValidatorEverScoped ),
                typeof( AsyncValidator ) );

            var authTypeSystem = new StdAuthenticationTypeSystem();
            var authInfo = authTypeSystem.AuthenticationInfo.Create( authTypeSystem.UserInfo.Create( 3712, "John" ), DateTime.UtcNow.AddDays( 1 ) );

            using var appServices = TestHelper.CreateAutomaticServices( c, configureServices: services =>
            {
                services.Services.AddScoped( s => authInfo );
            } ).Services;
            await TestHelper.StopHostedServicesAsync( appServices );

            using( var scope = appServices.CreateScope() )
            {
                var services = scope.ServiceProvider;

                var directory = services.GetRequiredService<CrisDirectory>();
                var validator = services.GetRequiredService<RawCrisValidator>();

                var cmd = services.GetRequiredService<IPocoFactory<ICmdTestSecure>>().Create( c => c.Value = 1 );
                var result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeFalse();
                result.Messages.Should().HaveCount( 3 )
                        .And.Contain( m => m.Level == UserMessageLevel.Error && m.Text == "Security error." )
                        .And.Contain( m => m.Level == UserMessageLevel.Info && m.Text == "AsyncValidator waiting for result..." )
                        .And.Contain( m => m.Level == UserMessageLevel.Info && m.Text == "AsyncValidator is fine." );

                cmd.ActorId = 3712;
                result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeTrue();
                result.HasWarnings.Should().BeFalse();
                result.Messages.Should().HaveCount( 2 )
                       .And.Contain( m => m.Level == UserMessageLevel.Info && m.Text == "AsyncValidator waiting for result..." )
                       .And.Contain( m => m.Level == UserMessageLevel.Info && m.Text == "AsyncValidator is fine." );

                cmd.Value = 0;
                result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeTrue();
                result.HasWarnings.Should().BeTrue();
                result.Messages.Should().HaveCount( 3 )
                       .And.Contain( m => m.Level == UserMessageLevel.Warn && m.Text == "[Scoped]A positive Value would be better." )
                       .And.Contain( m => m.Level == UserMessageLevel.Info && m.Text == "AsyncValidator waiting for result..." )
                       .And.Contain( m => m.Level == UserMessageLevel.Info && m.Text == "AsyncValidator is fine." );

                cmd.ActorId = 3712;
                cmd.WarnByAsyncValidator = true;
                result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeTrue();
                result.HasWarnings.Should().BeTrue();
                result.Messages.Should().HaveCount( 3 )
                       .And.Contain( m => m.Level == UserMessageLevel.Warn && m.Text == "[Scoped]A positive Value would be better." )
                       .And.Contain( m => m.Level == UserMessageLevel.Info && m.Text == "AsyncValidator waiting for result..." )
                       .And.Contain( m => m.Level == UserMessageLevel.Warn && m.Text == "AsyncValidator is not happy!" );

            }
        }

        public class ValidatorWithLogs : IAutoService
        {
            [CommandValidator]
            public void ValidateCommand( IActivityMonitor monitor, UserMessageCollector c, ITestCommand cmd )
            {
                monitor.Info( "I'm the ValidatorWithLogs." );
            }
        }

        [Test]
        public async Task Validators_can_log_if_they_want_Async()
        {
            var c = TestHelper.CreateStObjCollector(
                typeof( RawCrisValidator ), typeof( CrisDirectory ), typeof( ICrisResultError ), typeof( AmbientValues.IAmbientValues ),
                typeof( ITestCommand ),
                typeof( ValidatorWithLogs ) );

            using var appServices = TestHelper.CreateAutomaticServices( c ).Services;
            await TestHelper.StopHostedServicesAsync( appServices );

            using( var scope = appServices.CreateScope() )
            {
                var directory = scope.ServiceProvider.GetRequiredService<PocoDirectory>();
                var validator = scope.ServiceProvider.GetRequiredService<RawCrisValidator>();

                var cmd = directory.Create<ITestCommand>();

                using( TestHelper.Monitor.CollectTexts( out var logs ) )
                {
                    var result = await validator.ValidateCommandAsync( TestHelper.Monitor, scope.ServiceProvider, cmd );
                    result.Success.Should().BeTrue();
                    result.Messages.Should().BeEmpty();
                    logs.Should().HaveCount( 2 )
                                 .And.Contain( "Validating 'Test' command." )
                                 .And.Contain( "I'm the ValidatorWithLogs." );
                }

            }
        }
    }
}
