using CK.AppIdentity;
using CK.AspNet.Auth;
using CK.AspNet.Auth.Cris;
using CK.Auth;
using CK.Core;
using CK.Cris.AmbientValues;
using CK.Cris.AspNet;
using CK.Testing;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.HttpSender.Tests
{



    [TestFixture]
    public class SenderTests
    {
        [Test]
        public async Task sending_commands_Async()
        {
            // We need the fr culture for this test.
            NormalizedCultureInfo.EnsureNormalizedCultureInfo( "fr" );
            var serverEngineConfiguration = TestHelper.CreateDefaultEngineConfiguration();
            serverEngineConfiguration.FirstBinPath.Types.Add( typeof( IAuthenticationInfo ),
                                                              typeof( StdAuthenticationTypeSystem ),
                                                              typeof( AuthenticationInfoTokenService ),
                                                              typeof( CrisAuthenticationService ),
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
                                                              typeof( CrisAspNetService ),
                                                              typeof( CrisWebFrontAuthCommandHandler ),
                                                              typeof( FakeUserDatabase ),
                                                              typeof( FakeWebFrontLoginService ) );

            var serverMap = serverEngineConfiguration.RunSuccessfully().LoadMap();
            
            await using var runningServer = await serverMap.CreateAspNetAuthServerAsync( configureApplication: app => app.UseMiddleware<CrisMiddleware>() );

            var serverAddress = runningServer.ServerAddress;

            var callerEngineConfiguration = TestHelper.CreateDefaultEngineConfiguration();
            callerEngineConfiguration.FirstBinPath.Types.Add( typeof( IBeautifulWithOptionsCommand ),
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
                                                              typeof( CrisHttpSenderFeatureDriver ) );

            var callerMap = callerEngineConfiguration.RunSuccessfully().LoadMap();

            await using var runningCaller = await callerMap.CreateRunningCallerAsync( serverAddress, generateSourceCode: false );

            var callerPoco = runningCaller.Services.GetRequiredService<PocoDirectory>();
            var sender = runningCaller.ApplicationIdentityService.Remotes
                                                    .Single( r => r.PartyName == "$Server" )
                                                    .GetRequiredFeature<CrisHttpSender>();

            // ITotalCommand requires Normal authentication. 
            var totalCommand = callerPoco.Create<ITotalCommand>();
            // We don't have the AmbientValues here to apply them.
            // ActorId is set to its default 0 (this would have been the default value).
            totalCommand.ActorId = 0;
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
            totalCommand.ActorId = 9999999;
            totalExecutedCommand = await sender.SendAsync( TestHelper.Monitor, totalCommand );
            totalExecutedCommand.Result.Should().BeAssignableTo<ICrisResultError>();
            error = (ICrisResultError)totalExecutedCommand.Result!;
            error.IsValidationError.Should().BeTrue();
            error.Errors[0].Text.Should().StartWith( "Invalid actor identifier: " );

            // Albert (null current culture name): this is executed in the Global DI context.
            totalCommand.ActorId = 3712; 
            var totalResult = await sender.SendAndGetResultOrThrowAsync( TestHelper.Monitor, totalCommand );
            totalResult.Success.Should().BeTrue();
            totalResult.ActorId.Should().Be( 3712 );
            totalResult.CultureName.Should().Be( "en" );

            // Albert in French: this is executed in a Background job. 
            totalCommand.CurrentCultureName = "fr";
            totalResult = await sender.SendAndGetResultOrThrowAsync( TestHelper.Monitor, totalCommand );
            totalResult.Success.Should().BeTrue();
            totalResult.ActorId.Should().Be( 3712, "The authentication info has been transferred." );
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
                .Should().ThrowAsync<CKException>();
            //
            // Why does FluentAssertions now fails to match this?
            // It used to work and this is still correct :(.
            //
            //   .WithMessage( """
            //   - An unhandled error occurred while executing command 'CK.Cris.HttpSender.Tests.INakedCommand' (LogKey: *).
            //     -> *SenderTests.cs@*
            //   - Outer exception.
            //     - One or more errors occurred.
            //       - Bug! (n°1)
            //       - Bug! (n°2)
            //   """ );
        }

        [Test]
        public async Task retry_strategy_Async()
        {
            var callerEngineConfiguration = TestHelper.CreateDefaultEngineConfiguration();
            callerEngineConfiguration.FirstBinPath.Types.Add( typeof( IBeautifulWithOptionsCommand ),
                                                              typeof( CrisDirectory ),
                                                              typeof( CommonPocoJsonSupport ),
                                                              typeof( ApplicationIdentityService ),
                                                              typeof( ApplicationIdentityServiceConfiguration ),
                                                              typeof( CrisHttpSenderFeatureDriver ));
            var map = callerEngineConfiguration.RunSuccessfully().LoadMap();

            await using var runningCaller = await map.CreateRunningCallerAsync( "http://[::1]:65036/" );
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

    }
}
