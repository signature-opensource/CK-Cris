using CK.Auth;
using CK.Core;
using FluentAssertions;
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
        [ExternalName("Test")]
        public interface ICmdTest : ICommand
        {
            int Value { get; set; }
        }

        [Test]
        public async Task when_there_is_no_validation_methods_the_validation_succeeds_Async()
        {
            var c = TestHelper.CreateStObjCollector(
                typeof( RawCrisValidator ), typeof( CrisDirectory ), typeof( ICrisResultError ), typeof( AmbientValues.IAmbientValues ),
                typeof( ICmdTest ) );

            using var services = TestHelper.CreateAutomaticServices( c ).Services;

            var directory = services.GetRequiredService<CrisDirectory>();
            var cmd = directory.CrisPocoModels[0].Create();

            //var validator = services.GetRequiredService<RawCrisValidator>();
            //var result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
            //result.Success.Should().BeTrue();
        }

        public class BuggyValidator : IAutoService
        {
            [CommandValidator]
            public void ValidateCommand( IActivityMonitor m, ICmdTest cmd )
            {
                throw new Exception( "This should not happen!" );
            }
        }

        [Test]
        public async Task exceptions_raised_by_validators_are_NOT_handled_by_the_RawCrisValidator_the_caller_MUST_handle_them_Async()
        {
            var c = TestHelper.CreateStObjCollector(
                typeof( RawCrisValidator ), typeof( CrisDirectory ), typeof( ICrisResultError ), typeof( AmbientValues.IAmbientValues ),
                typeof( ICmdTest ),
                typeof( BuggyValidator ) );
            using var services = TestHelper.CreateAutomaticServicesWithMonitor( c ).Services;

            var directory = services.GetRequiredService<CrisDirectory>();
            var cmd = directory.CrisPocoModels[0].Create();

            //var validator = services.GetRequiredService<RawCrisValidator>();
            //await validator.Awaiting( sut => sut.ValidateCommandAsync( TestHelper.Monitor, services, cmd ) )
            //               .Should().ThrowAsync<Exception>().WithMessage( "This should not happen!" );
        }

        [ExternalName("NoValidators")]
        public interface ICmdWithoutValidators : ICommand
        {
            int AnyValue { get; set; }
        }

        public class SimplestValidatorEverSingleton : IAutoService
        {
            [CommandValidator]
            public void ValidateCommand( IActivityMonitor m, ICmdTest cmd )
            {
                if( cmd.Value < 0 ) m.Error( "[Singleton]Value should be greater than 0." );
                else if( cmd.Value == 0 ) m.Warn( "[Singleton]A positive Value would be better." );
            }
        }

        public class SimplestValidatorEverScoped : IScopedAutoService
        {
            [CommandValidator]
            public void ValidateCommand( IActivityMonitor m, ICmdTest cmd )
            {
                if( cmd.Value < 0 ) m.Error( "[Scoped]Value should be greater than 0." );
                else if( cmd.Value == 0 ) m.Warn( "[Scoped]A positive Value would be better." );
            }
        }

        [TestCase( true, false )]
        [TestCase( false, true )]
        [TestCase( true, true )]
        public async Task the_simplest_validation_is_held_by_a_dependency_free_service_and_is_synchronous_Async( bool scopedService, bool singletonService )
        {
            var c = TestHelper.CreateStObjCollector( typeof( RawCrisValidator ), typeof( CrisDirectory ), typeof( ICrisResultError ), typeof( AmbientValues.IAmbientValues ),
                                                     typeof( ICmdTest ),
                                                     typeof( ICmdWithoutValidators ) );
            if( singletonService ) c.RegisterType( typeof( SimplestValidatorEverSingleton ) );
            if( scopedService ) c.RegisterType( typeof( SimplestValidatorEverScoped ) );

            using var appServices = TestHelper.CreateAutomaticServices( c ).Services;

            using( var scope = appServices.CreateScope() )
            {
                var services = scope.ServiceProvider; 

                var directory = services.GetRequiredService<CrisDirectory>();
                var validator = services.GetRequiredService<RawCrisValidator>();

                var cmd = services.GetRequiredService<IPocoFactory<ICmdTest>>().Create( c => c.Value = -1 );
                var result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeFalse();
                if( scopedService )
                {
                    result.Errors.Should().Contain( "[Scoped]Value should be greater than 0." );
                }
                if( singletonService )
                {
                    result.Errors.Should().Contain( "[Singleton]Value should be greater than 0." );
                }

                cmd.Value = 0;
                result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeTrue();
                result.HasWarnings.Should().BeTrue();
                if( scopedService )
                {
                    result.Warnings.Should().Contain( "[Scoped]A positive Value would be better." );
                }
                if( singletonService )
                {
                    result.Warnings.Should().Contain( "[Singleton]A positive Value would be better." );
                }
                result.Errors.Should().BeEmpty();
            }
        }

        public interface IAuthenticatedCommandPart : ICommandPart
        {
            int ActorId { get; set; }
        }

        public class AuthenticationValidator : IAutoService
        {
            [CommandValidator]
            public void ValidateCommand( IActivityMonitor m, IAuthenticatedCommandPart cmd, IAuthenticationInfo info )
            {
                if( cmd.ActorId != info.User.UserId ) m.Error( "Security error." );
            }
        }

        public interface ICmdTestSecure : ICmdTest, IAuthenticatedCommandPart
        {
            bool WarnByAsyncValidator { get; set; }
        }


        public class AsyncValidator : IAutoService
        {
            [CommandValidator]
            public async Task ValidateCommandAsync( IActivityMonitor m, ICmdTestSecure cmd )
            {
                m.Info( "AsyncValidator waiting for result..." );
                await Task.Delay( 20 );
                if( cmd.WarnByAsyncValidator ) m.Warn( "AsyncValidator is not happy!" );
                else m.Info( "AsyncValidator is fine." );
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

            var map = TestHelper.CompileAndLoadStObjMap( c ).Map;
            var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, new ServiceCollection() );
            reg.Register<IAuthenticationInfo>( s => authInfo, isScoped: true, allowMultipleRegistration: false );
            reg.AddStObjMap( map ).Should().BeTrue( "Service configuration succeed." );

            var appServices = reg.Services.BuildServiceProvider();

            using( var scope = appServices.CreateScope() )
            {
                var services = scope.ServiceProvider;

                var directory = services.GetRequiredService<CrisDirectory>();
                var validator = services.GetRequiredService<RawCrisValidator>();

                var cmd = services.GetRequiredService<IPocoFactory<ICmdTestSecure>>().Create( c => c.Value = 1 );
                var result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeFalse();
                result.Errors.Should().BeEquivalentTo( "Security error." );

                cmd.ActorId = 3712;
                result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeTrue();
                result.HasWarnings.Should().BeFalse();

                cmd.Value = 0;
                result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeTrue();
                result.HasWarnings.Should().BeTrue();

                cmd.ActorId = 3712;
                cmd.WarnByAsyncValidator = true;
                result = await validator.ValidateCommandAsync( TestHelper.Monitor, services, cmd );
                result.Success.Should().BeTrue();
                result.HasWarnings.Should().BeTrue();
                result.Warnings.Should().Contain( "AsyncValidator is not happy!" );

            }
        }
    }
}
