using CK.AppIdentity;
using CK.AspNet.Auth;
using CK.AspNet.Auth.Cris;
using CK.Auth;
using CK.Core;
using CK.Cris.AmbientValues;
using CK.Cris.AspNet;
using CK.Testing.StObjEngine;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.HttpSender.Tests
{

    [TestFixture]
    public class SenderTests
    {
        [Test]
        public async Task sending_commands_Async()
        {
            await using var runningServer = await TestHelper.RunAspNetServerAsync(
                                                new[]
                                                {
                                                    typeof( IAuthenticationInfo ),
                                                    typeof( CrisAspNetService ),
                                                    typeof( IBeautifulWithOptionsCommand ),
                                                    typeof( INakedCommand ),
                                                    typeof( AmbientValuesService ),
                                                    typeof( ColorAndNakedService ),
                                                    typeof( WithOptionsService ),
                                                    typeof( ITotalCommand ),
                                                    typeof( ITotalResult ),
                                                    typeof( TotalCommandService ),
                                                    typeof( IBasicLoginCommand ),
                                                    typeof( ILogoutCommand ),
                                                    typeof( IRefreshAuthenticationCommand ),
                                                    typeof( IAuthenticationResult ),
                                                    typeof( IPocoAuthenticationInfo ),
                                                    typeof( IPocoUserInfo ),
                                                    typeof( CrisAuthenticationService ),
                                                    typeof( CrisWebFrontAuthCommandHandler )
                                                },
                                                configureServices: services =>
                                                {
                                                    // We could have used the type registration above and
                                                    // benefit of the Automatic DI.
                                                    services.AddSingleton<FakeWebFrontLoginService>();
                                                    services.AddSingleton<IWebFrontAuthLoginService>( sp => sp.GetRequiredService<FakeWebFrontLoginService>() );
                                                } );

            var serverAddress = runningServer.ServerAddress;

            var callerServices = new[] { typeof( IBeautifulWithOptionsCommand ),
                                         typeof( INakedCommand ),
                                         typeof( ITotalCommand ),
                                         typeof( ITotalResult ),
                                         typeof( IBasicLoginCommand ),
                                         typeof( ILogoutCommand ),
                                         typeof( IRefreshAuthenticationCommand ),
                                         typeof( IAuthenticationResult ),
                                         typeof( IPocoAuthenticationInfo ),
                                         typeof( IPocoUserInfo ),
                                         typeof( CrisDirectory ),
                                         typeof( CommonPocoJsonSupport ),
                                         typeof( ApplicationIdentityService ),
                                         typeof( CrisHttpSenderFeatureDriver )};

            await using var runningCaller = await CreateRunningCallerAsync( serverAddress, callerServices, generateSourceCode: false );

            var callerPoco = runningCaller.Services.GetRequiredService<PocoDirectory>();
            var sender = runningCaller.ApplicationIdentityService.Remotes
                                                    .Single( r => r.PartyName == "$Server" )
                                                    .GetRequiredFeature<CrisHttpSender>();

            // ITotalCommand requires Normal authentication. 
            var totalCommand = callerPoco.Create<ITotalCommand>();
            var totalExecutedCommand = await sender.SendAsync( TestHelper.Monitor, totalCommand );
            totalExecutedCommand.Result.Should().BeAssignableTo<ICrisResultError>();
            var error = (ICrisResultError)totalExecutedCommand.Result!;
            error.IsValidationError.Should().BeTrue();
            error.Errors[0].Text.Should().StartWith( "Invalid authentication level: " );

            var loginCommand = callerPoco.Create<IBasicLoginCommand>( c =>
            {
                c.UserName = "Albert";
                c.Password = "success";
            } );

            var loginAlbert = await sender.SendAndGetResultOrThrowAsync( TestHelper.Monitor, loginCommand );
            loginAlbert.Info.User.UserName.Should().Be( "Albert" );

            // Unexisting user id.
            totalCommand.ActorId = 3712;
            totalExecutedCommand = await sender.SendAsync( TestHelper.Monitor, totalCommand );
            totalExecutedCommand.Result.Should().BeAssignableTo<ICrisResultError>();
            error = (ICrisResultError)totalExecutedCommand.Result!;
            error.IsValidationError.Should().BeTrue();
            error.Errors[0].Text.Should().StartWith( "Invalid actor identifier: " );

            // Albert (null current culture name): this is executed in the Global DI context.
            totalCommand.ActorId = 2; 
            var totalResult = await sender.SendAndGetResultOrThrowAsync( TestHelper.Monitor, totalCommand );
            totalResult.Success.Should().BeTrue();
            totalResult.ActorId.Should().Be( 2 );
            totalResult.CultureName.Should().Be( "en" );

            // Albert in French: this is executed in a Background job. 
            totalCommand.CurrentCultureName = "fr";
            totalResult = await sender.SendAndGetResultOrThrowAsync( TestHelper.Monitor, totalCommand );
            totalResult.Success.Should().BeTrue();
            totalResult.ActorId.Should().Be( 2, "The authentication info has been transferred." );
            totalResult.CultureName.Should().Be( "fr", "The current culture is French." );

            // Albert in French sends an invalid action.
            totalCommand.Action = "Invalid";
            totalExecutedCommand = await sender.SendAsync( TestHelper.Monitor, totalCommand );
            totalExecutedCommand.Result.Should().BeAssignableTo<ICrisResultError>();
            error = (ICrisResultError)totalExecutedCommand.Result!;
            error.IsValidationError.Should().BeTrue();
            error.Errors[0].Text.Should().StartWith( "The Action must be Bug!, Error!, Warn! or empty. Not 'Invalid'." );

            // Logout.
            await sender.SendOrThrowAsync( TestHelper.Monitor, callerPoco.Create<ILogoutCommand>() );

            await TestSimpleCommandsAsync( callerPoco, sender );

            await TestAuthenticationCommandsAsync( callerPoco, sender );

        }

        static async Task TestAuthenticationCommandsAsync( PocoDirectory callerPoco, CrisHttpSender sender )
        {
            sender.AuthorizationToken.Should().BeNull( "No authentication token." );

            var loginCommand = callerPoco.Create<IBasicLoginCommand>( c =>
            {
                c.UserName = "Albert";
                c.Password = "success";
            } );

            var initialAuth = await sender.SendAndGetResultOrThrowAsync( TestHelper.Monitor, loginCommand );
            initialAuth.Success.Should().BeTrue();
            initialAuth.Info.Level.Should().Be( AuthLevel.Normal );
            initialAuth.Info.User.UserName.Should().Be( "Albert" );
            sender.AuthorizationToken.Should().NotBeNull( "The AuthorizationToken is set." );

            var refreshCommand = callerPoco.Create<IRefreshAuthenticationCommand>();
            var refreshedAuth = await sender.SendAndGetResultOrThrowAsync( TestHelper.Monitor, refreshCommand );
            refreshedAuth.Success.Should().BeTrue();
            refreshedAuth.Info.User.UserName.Should().Be( "Albert" );

            var logoutCommand = callerPoco.Create<ILogoutCommand>();
            await sender.SendOrThrowAsync( TestHelper.Monitor, logoutCommand );
            sender.AuthorizationToken.Should().BeNull( "No more AuthorizationToken." );
        }

        static async Task TestSimpleCommandsAsync( PocoDirectory callerPoco, CrisHttpSender sender )
        {
            // Command with result.
            var cmd = callerPoco.Create<IBeautifulCommand>( c =>
            {
                c.Color = "Black";
                c.Beauty = "Marvellous";
            } );
            var result = await sender.SendAsync( TestHelper.Monitor, cmd );
            result.Result.Should().Be( "Black - Marvellous - 0" );

            // Command without result.
            var naked = callerPoco.Create<INakedCommand>( c => c.Event = "Something" );
            var nakedResult = await sender.SendAsync( TestHelper.Monitor, naked );
            nakedResult.Result.Should().BeNull();

            // Command without result that throws.
            var nakedBug = callerPoco.Create<INakedCommand>( c => c.Event = "Bug!" );
            var nakedBugResult = await sender.SendAsync( TestHelper.Monitor, nakedBug );
            nakedBugResult.Result.Should().NotBeNull().And.BeAssignableTo<ICrisResultError>();

            // Command without result that throws and use SendOrThrowAsync.
            var nakedBug2 = callerPoco.Create<INakedCommand>( c => c.Event = "Bug!" );
            await FluentActions.Awaiting( () => sender.SendOrThrowAsync( TestHelper.Monitor, nakedBug2 ) )
                .Should().ThrowAsync<CKException>()
                .WithMessage( """
                - An unhandled error occurred while executing command 'CK.Cris.HttpSender.Tests.INakedCommand' (LogKey: *).
                  -> *SenderTests.cs@*
                - Outer exception.
                  - One or more errors occurred.
                    - Bug! (n°1)
                    - Bug! (n°2)
                """ );
        }

        [Test]
        public async Task retry_strategy_Async()
        {
            var callerServices = new[] { typeof( IBeautifulWithOptionsCommand ),
                                         typeof( CrisDirectory ),
                                         typeof( CommonPocoJsonSupport ),
                                         typeof( ApplicationIdentityService ),
                                         typeof( ApplicationIdentityServiceConfiguration ),
                                         typeof( CrisHttpSenderFeatureDriver )};
            await using var runningCaller = await CreateRunningCallerAsync( "http://[::1]:65036/", callerServices );
            var callerPoco = runningCaller.Services.GetRequiredService<PocoDirectory>();
            var sender = runningCaller.ApplicationIdentityService.Remotes
                                                    .Single( r => r.PartyName == "$Server" )
                                                    .GetRequiredFeature<CrisHttpSender>();
            var cmd = callerPoco.Create<IBeautifulCommand>( c =>
            {
                c.Color = "Black";
                c.Beauty = "Marvellous";
            } );

            using( TestHelper.Monitor.CollectTexts( out var logs ) )
            {
                var result = await sender.SendAsync( TestHelper.Monitor, cmd );
                logs.Should()
                    .Contain( """Sending ["CK.Cris.HttpSender.Tests.IBeautifulCommand",{"beauty":"Marvellous","waitTime":0,"color":"Black"}] to 'Domain/$Server/#Dev'.""" )
                    .And.Contain( """Request failed on 'Domain/$Server/#Dev' (attempt n°0).""" )
                    .And.Contain( """Request failed on 'Domain/$Server/#Dev' (attempt n°1).""" )
                    .And.Contain( """Request failed on 'Domain/$Server/#Dev' (attempt n°2).""" )
                    .And.Contain( """While sending: ["CK.Cris.HttpSender.Tests.IBeautifulCommand",{"beauty":"Marvellous","waitTime":0,"color":"Black"}]""" );
            }
        }


        static Task<ApplicationIdentityTestHelperExtension.RunningAppIdentity> CreateRunningCallerAsync( string serverAddress,
                                                                                                         Type[] registerTypes,
                                                                                                         Action<MutableConfigurationSection>? configuration = null,
                                                                                                         bool generateSourceCode = true )
        {
            var callerCollector = TestHelper.CreateStObjCollector( registerTypes );
            var callerMap = TestHelper.CompileAndLoadStObjMap( callerCollector, engineConfigurator: c =>
            {
                c.BinPaths[0].GenerateSourceFiles = generateSourceCode;
                return c;
            } ).Map;

            return TestHelper.CreateRunningAppIdentityServiceAsync(
                c =>
                {
                    c["FullName"] = "Domain/$Caller";
                    c["Parties:0:FullName"] = "Domain/$Server";
                    c["Parties:0:Address"] = serverAddress;
                    c["Parties:0:CrisHttpSender"] = "true";
                    configuration?.Invoke( c );
                },
                services =>
                {
                    services.AddStObjMap( TestHelper.Monitor, callerMap );
                } );
        }

    }
}
