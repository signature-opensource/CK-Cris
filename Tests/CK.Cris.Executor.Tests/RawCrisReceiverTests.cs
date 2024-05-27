using CK.Auth;
using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;
using static System.Formats.Asn1.AsnWriter;

namespace CK.Cris.Executor.Tests
{
    [TestFixture]
    public class RawCrisReceiverTests
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
                typeof( RawCrisReceiver ), typeof( CrisDirectory ), typeof( ICrisResultError ), typeof( AmbientValues.IAmbientValues ),
                typeof( ITestCommand ) );

            using var services = TestHelper.CreateAutomaticServices( c ).Services;
            await TestHelper.StartHostedServicesAsync( services );

            var directory = services.GetRequiredService<PocoDirectory>();
            var cmd = directory.Create<ITestCommand>();

            var validator = services.GetRequiredService<RawCrisReceiver>();
            var result = await validator.IncomingValidateAsync( TestHelper.Monitor, services, cmd );
            result.Success.Should().BeTrue();
        }

        public class BuggyValidator : IAutoService
        {
            [IncomingValidator]
            public void ValidateCommand( UserMessageCollector c, ITestCommand cmd )
            {
                throw new Exception( "This should not happen!" );
            }
        }

        [Test]
        public async Task exceptions_raised_by_validators_are_handled_by_the_RawCrisReceiver_Async()
        {
            // To leak all exceptions in messages, CoreApplicationIdentity must be initialized and be in "#Dev" environment name.  
            CoreApplicationIdentity.Initialize();

            var c = TestHelper.CreateStObjCollector( typeof( RawCrisReceiver ), typeof( CrisDirectory ),
                                                     typeof( ITestCommand ),
                                                     typeof( BuggyValidator ) );
            using var services = TestHelper.CreateAutomaticServicesWithMonitor( c ).Services;
            await TestHelper.StartHostedServicesAsync( services );

            var directory = services.GetRequiredService<PocoDirectory>();
            var cmd = directory.Create<ITestCommand>();

            var validator = services.GetRequiredService<RawCrisReceiver>();
            var result = await validator.IncomingValidateAsync( TestHelper.Monitor, services, cmd );
            result.Success.Should().BeFalse();
            result.ErrorMessages.Should().HaveCount( 2 )
                       .And.Contain( m => m.Level == UserMessageLevel.Error
                                          && m.ResName == "Cris.UnhandledValidationError"
                                          && m.Text.StartsWith( "An unhandled error occurred while validating command 'Test' (LogKey:" ) )
                       .And.Contain( m => m.Level == UserMessageLevel.Error
                                          && m.ResName == "SHA.3pSE_AMsQ-7QNQP6WgA9j88mLSI"
                                          && m.Message == "This should not happen!" );
        }

        [ExternalName( "NoValidators" )]
        public interface IWithoutValidatorsCommand : ICommand
        {
            int AnyValue { get; set; }
        }

        public class SimplestValidatorEverSingleton : IAutoService
        {
            [IncomingValidator]
            public void ValidateCommand( UserMessageCollector c, ITestCommand cmd )
            {
                if( cmd.Value < 0 ) c.Error( "[Singleton]Value should be greater than 0." );
                else if( cmd.Value == 0 ) c.Warn( "[Singleton]A positive Value would be better." );
            }
        }

        public class SimplestValidatorEverScoped : IScopedAutoService
        {
            [IncomingValidator]
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
            var c = TestHelper.CreateStObjCollector( typeof( RawCrisReceiver ), typeof( CrisDirectory ),
                                                     typeof( ITestCommand ),
                                                     typeof( IWithoutValidatorsCommand ) );
            if( singletonService ) c.RegisterType( TestHelper.Monitor, typeof( SimplestValidatorEverSingleton ) );
            if( scopedService ) c.RegisterType( TestHelper.Monitor, typeof( SimplestValidatorEverScoped ) );

            using var appServices = TestHelper.CreateAutomaticServices( c ).Services;
            await TestHelper.StopHostedServicesAsync( appServices );

            using( var scope = appServices.CreateScope() )
            {
                var services = scope.ServiceProvider;

                var directory = services.GetRequiredService<CrisDirectory>();
                var validator = services.GetRequiredService<RawCrisReceiver>();

                var cmd = services.GetRequiredService<IPocoFactory<ITestCommand>>().Create( c => c.Value = -1 );
                var result = await validator.IncomingValidateAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeFalse();
                if( scopedService )
                {
                    result.ValidationMessages.Should().Contain( m => m.Level == UserMessageLevel.Error && m.Text == "[Scoped]Value should be greater than 0." );
                }
                if( singletonService )
                {
                    result.ValidationMessages.Should().Contain( m => m.Level == UserMessageLevel.Error && m.Text == "[Singleton]Value should be greater than 0." );
                }

                cmd.Value = 0;
                result = await validator.IncomingValidateAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeTrue();
                result.ValidationMessages.Any( m => m.Level == UserMessageLevel.Warn ).Should().BeTrue();
                if( scopedService )
                {
                    result.ValidationMessages.Should().Contain( m => m.Level == UserMessageLevel.Warn && m.Text == "[Scoped]A positive Value would be better." );
                }
                if( singletonService )
                {
                    result.ValidationMessages.Should().Contain( m => m.Level == UserMessageLevel.Warn && m.Text == "[Singleton]A positive Value would be better." );
                }
                result.ValidationMessages.Should().NotContain( m => m.Level == UserMessageLevel.Error );
            }
        }

        public interface ICommandAuthenticatedPart : ICommandPart
        {
            int ActorId { get; set; }
        }

        public class AuthenticationValidator : IAutoService
        {
            [IncomingValidator]
            public void ValidateCommand( UserMessageCollector c, ICommandAuthenticatedPart cmd, IAuthenticationInfo info )
            {
                if( cmd.ActorId != info.User.UserId ) c.Error( "Security error." );
            }
        }

        public interface ITestSecureCommand : ITestCommand, ICommandAuthenticatedPart
        {
            bool WarnByAsyncValidator { get; set; }
        }


        public class AsyncValidator : IAutoService
        {
            [IncomingValidator]
            public async Task ValidateCommandAsync( UserMessageCollector c, ITestSecureCommand cmd )
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
                typeof( RawCrisReceiver ), typeof( CrisDirectory ), typeof( ICrisResultError ), typeof( AmbientValues.IAmbientValues ),
                typeof( ITestSecureCommand ),
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
                var validator = services.GetRequiredService<RawCrisReceiver>();

                var cmd = services.GetRequiredService<IPocoFactory<ITestSecureCommand>>().Create( c => c.Value = 1 );
                var result = await validator.IncomingValidateAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeFalse();
                result.ValidationMessages.Should().HaveCount( 3 )
                        .And.Contain( m => m.Level == UserMessageLevel.Error && m.Text == "Security error." )
                        .And.Contain( m => m.Level == UserMessageLevel.Info && m.Text == "AsyncValidator waiting for result..." )
                        .And.Contain( m => m.Level == UserMessageLevel.Info && m.Text == "AsyncValidator is fine." );

                cmd.ActorId = 3712;
                result = await validator.IncomingValidateAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeTrue();
                result.ValidationMessages.Any( m => m.Level == UserMessageLevel.Warn ).Should().BeFalse();
                result.ValidationMessages.Should().HaveCount( 2 )
                       .And.Contain( m => m.Level == UserMessageLevel.Info && m.Text == "AsyncValidator waiting for result..." )
                       .And.Contain( m => m.Level == UserMessageLevel.Info && m.Text == "AsyncValidator is fine." );

                cmd.Value = 0;
                result = await validator.IncomingValidateAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeTrue();
                result.ValidationMessages.Should().HaveCount( 3 )
                       .And.Contain( m => m.Level == UserMessageLevel.Warn && m.Text == "[Scoped]A positive Value would be better." )
                       .And.Contain( m => m.Level == UserMessageLevel.Info && m.Text == "AsyncValidator waiting for result..." )
                       .And.Contain( m => m.Level == UserMessageLevel.Info && m.Text == "AsyncValidator is fine." );

                cmd.ActorId = 3712;
                cmd.WarnByAsyncValidator = true;
                result = await validator.IncomingValidateAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeTrue();
                result.ValidationMessages.Should().HaveCount( 3 )
                       .And.Contain( m => m.Level == UserMessageLevel.Warn && m.Text == "[Scoped]A positive Value would be better." )
                       .And.Contain( m => m.Level == UserMessageLevel.Info && m.Text == "AsyncValidator waiting for result..." )
                       .And.Contain( m => m.Level == UserMessageLevel.Warn && m.Text == "AsyncValidator is not happy!" );

            }
        }

        public class ValidatorWithLogs : IAutoService
        {
            [IncomingValidator]
            public void IncomingValidateCommand( IActivityMonitor monitor, UserMessageCollector c, ITestCommand cmd )
            {
                monitor.Info( "I'm the (Incoming) ValidatorWithLogs." );
            }

            [CommandHandlingValidator]
            public void ValidateCommand( IActivityMonitor monitor, UserMessageCollector c, ITestCommand cmd )
            {
                monitor.Info( "I'm the (CommandHandling) ValidatorWithLogs." );
            }

            // Required handler to test the CommandHandlingValidator because when a command has no handler:
            // ==> Command 'Test' is not handled. Forgetting 1 validator, 0 ambient service configurators, 0 ambient service restorers and 0 post handlers.
            [CommandHandler]
            public void HandleCommand( ITestCommand cmd )
            {
            }
        }

        [Test]
        public async Task Validators_can_log_if_they_want_Async()
        {
            var c = TestHelper.CreateStObjCollector(
                typeof( RawCrisReceiver ), typeof( RawCrisExecutor ), typeof( CrisDirectory ),
                typeof( ITestCommand ),
                typeof( ValidatorWithLogs ) );

            using var appServices = TestHelper.CreateAutomaticServices( c, configureServices: services =>
            {
                services.Services.AddScoped<IActivityMonitor, ActivityMonitor>();
                services.Services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
            } ).Services;
            // Triggers a resolution of IEnumerable<IHostedService>: this is enough to setup the DI containers.
            appServices.GetServices<IHostedService>();

            using( var scope = appServices.CreateScope() )
            {
                var monitor = scope.ServiceProvider.GetRequiredService<IActivityMonitor>();
                var directory = scope.ServiceProvider.GetRequiredService<PocoDirectory>();
                var validator = scope.ServiceProvider.GetRequiredService<RawCrisReceiver>();
                var executor = scope.ServiceProvider.GetRequiredService<RawCrisExecutor>();

                var cmd = directory.Create<ITestCommand>();

                using( monitor.CollectTexts( out var logs ) )
                {
                    CrisValidationResult result = await validator.IncomingValidateAsync( monitor, scope.ServiceProvider, cmd );
                    result.Success.Should().BeTrue();
                    result.ValidationMessages.Should().BeEmpty();
                    logs.Should().HaveCount( 1 )
                                 .And.Contain( "I'm the (Incoming) ValidatorWithLogs." );

                    await executor.RawExecuteAsync( scope.ServiceProvider, cmd );
                    logs.Should().HaveCount( 2 )
                                 .And.Contain( "I'm the (Incoming) ValidatorWithLogs." )
                                 .And.Contain( "I'm the (CommandHandling) ValidatorWithLogs." );
                }

            }
        }

        public class ValidatorWithBoth : IAutoService
        {
            [IncomingValidator]
            public void IncomingValidateCommand( UserMessageCollector messages, ICrisIncomingValidationContext context, ITestCommand cmd )
            {
                context.Messages.Should().BeSameAs( messages );
                messages.Info( "Validated!" );
            }
        }


        [Test]
        public async Task IncomingValidators_can_have_both_UserMessageCollector_and_ICrisIncomingValidationContext_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( RawCrisReceiver ), typeof( CrisDirectory ), typeof( ITestCommand ), typeof( ValidatorWithBoth ) );
            using var appServices = TestHelper.CreateAutomaticServices( c ).Services;
            // Triggers a resolution of IEnumerable<IHostedService>: this is enough to setup the DI containers.
            appServices.GetServices<IHostedService>();

            using( var scope = appServices.CreateScope() )
            {
                var directory = scope.ServiceProvider.GetRequiredService<PocoDirectory>();
                var validator = scope.ServiceProvider.GetRequiredService<RawCrisReceiver>();

                var cmd = directory.Create<ITestCommand>();

                CrisValidationResult result = await validator.IncomingValidateAsync( TestHelper.Monitor, scope.ServiceProvider, cmd );
                result.Success.Should().BeTrue();
                result.ValidationMessages.Select( m => m.Text ).Should().HaveCount( 1 )
                                    .And.Contain( "Validated!" );

            }
        }

        public class InvalidValidator : IAutoService
        {
            [IncomingValidator]
            public void IncomingValidateCommand( IActivityMonitor monitor, ITestCommand cmd )
            {
                monitor.Info( "I'm the (Incoming) ValidatorWithLogs." );
            }
        }

        [Test]
        public void IncomingValidators_requires_UserMessageCollector_or_ICrisIncomingValidationContext()
        {
            var c = TestHelper.CreateStObjCollector( typeof( RawCrisReceiver ), typeof( CrisDirectory ), typeof( ITestCommand ), typeof( InvalidValidator ) );
            TestHelper.GetFailedAutomaticServicesConfiguration( c, "[IncomingValidator] method 'InvalidValidator.IncomingValidateCommand( IActivityMonitor monitor, ITestCommand cmd )' must take a 'UserMessageCollector' and/or a 'ICrisIncomingValidationContext' parameter to collect validation errors, warnings and informations." );
        }
    }
}
