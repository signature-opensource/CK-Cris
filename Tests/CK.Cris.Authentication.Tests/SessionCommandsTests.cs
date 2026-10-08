using CK.Auth;
using CK.Core;
using CK.IO.Authentication;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;
// The CK.IO.Auth package (until republished) brings the legacy CK.Auth.Abstractions types.
using AuthLevel = CK.Core.AuthLevel;
using IUserInfoProvider = CK.Core.IUserInfoProvider;
using UserSchemeInfo = CK.Core.UserSchemeInfo;

namespace CK.Cris.Authentication.Tests;

/// <summary>
/// First engine check of the CK.IO.Authentication types (they cannot be tested in CK-Cris-Abstractions).
/// The handlers are fakes: the real ones belong to the authentication center.
/// </summary>
[TestFixture]
public class SessionCommandsTests
{
    public sealed class NoUser : IUserInfoProvider
    {
        public ValueTask<UserInfo?> GetUserInfoAsync( IActivityMonitor monitor, int userId ) => ValueTask.FromResult<UserInfo?>( null );
    }

    public sealed class FakeAuthenticationCenter : ISingletonAutoService
    {
        static ISessionResult Result( ICommand<ISessionResult> c, AuthenticationInfo info )
        {
            return c.CreateResult( r =>
            {
                r.Info.InitializeFrom( info );
                r.Token = "the-token";
            } );
        }

        [CommandHandler] public ISessionResult Handle( IPasswordLoginCommand c, AuthenticationInfo info ) => Result( c, info );
        [CommandHandler] public RedirectedUrl Handle( IStartLoginCommand c ) => new RedirectedUrl( "https://idp/" + c.Scheme, c.InlineMode );
        [CommandHandler] public ISessionResult Handle( IWeakLoginCommand c, AuthenticationInfo info ) => Result( c, info );
        [CommandHandler] public ISessionResult Handle( IRefreshSessionCommand c, AuthenticationInfo info ) => Result( c, info );
        [CommandHandler] public void Handle( ILogoutSessionCommand c ) { }
        [CommandHandler] public ISessionResult Handle( IImpersonateCommand c, AuthenticationInfo info ) => Result( c, info );
        [CommandHandler] public ISessionResult Handle( IClearImpersonationCommand c, AuthenticationInfo info ) => Result( c, info );
        [CommandHandler] public IList<string> Handle( IGetLoginSchemesQCommand c ) => ["Basic", "Weak"];
    }

    [Test]
    public async Task the_session_commands_are_valid_Cris_commands_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CrisAuthenticationService ),
                                              typeof( AuthenticationInfo ),
                                              typeof( AuthenticationInfoDefaultProvider ),
                                              typeof( NoUser ),
                                              typeof( RawCrisExecutor ),
                                              typeof( RawCrisReceiver ),
                                              typeof( CrisDirectory ),
                                              typeof( ICrisResultError ),
                                              typeof( IPasswordLoginCommand ),
                                              typeof( IStartLoginCommand ),
                                              typeof( IWeakLoginCommand ),
                                              typeof( IRefreshSessionCommand ),
                                              typeof( ILogoutSessionCommand ),
                                              typeof( IImpersonateCommand ),
                                              typeof( IClearImpersonationCommand ),
                                              typeof( IGetLoginSchemesQCommand ),
                                              typeof( ISessionResult ),
                                              typeof( IAuthenticationInfoPoco ),
                                              typeof( IUserInfoPoco ),
                                              typeof( FakeAuthenticationCenter ),
                                              typeof( IAuthenticationSessionEndpoint ) );
        var albert = new UserInfo( 3712, "Albert", AuthLevel.Critical, [new UserSchemeInfo( "Basic", new DateTime( 2000, 1, 1, 0, 0, 0, DateTimeKind.Utc ) )] );
        var session = AuthenticationInfo.Create( albert, DateTime.UtcNow.AddDays( 1 ), null, "D", DateTime.UtcNow );
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices( configureServices: services =>
            {
                services.AddScoped( s => session );
            } );
            texts.ShouldNotContain( t => t.Contains( "is checked by no [IncomingValidator]" ), "The CrisAuthenticationService validates the parts." );

            using var scope = auto.Services.CreateScope();
            var services = scope.ServiceProvider;
            var poco = services.GetRequiredService<PocoDirectory>();
            var executor = services.GetRequiredService<RawCrisExecutor>();

            var login = poco.Create<IPasswordLoginCommand>( c => { c.UserName = "Albert"; c.Password = "secret-password"; } );
            login.ShouldBeAssignableTo<ICommandEndpointOnly>();
            login.CrisPocoModel.HasSensitiveData.ShouldBeTrue();

            var r = (ISessionResult)(await executor.RawExecuteAsync( services, login )).Result!;
            r.Token.ShouldBe( "the-token" );
            r.Info.UnsafeUser.UserId.ShouldBe( 3712 );
            r.Info.UnsafeUser.UserName.ShouldBe( "Albert" );
            r.Info.UnsafeUser.Level.ShouldBe( AuthLevel.Normal );
            r.Info.UnsafeUser.Schemes.ShouldHaveSingleItem().Scheme.ShouldBe( "Basic" );
            r.Info.DeviceId.ShouldBe( "D" );

            var start = poco.Create<IStartLoginCommand>( c => { c.Scheme = "Oidc.Azure"; c.InlineMode = true; c.UserData.Add( "InvitationToken", "T" ); } );
            start.CrisPocoModel.ResultType.ShouldBe( typeof( RedirectedUrl ) );
            (await executor.RawExecuteAsync( services, start )).Result.ShouldBe( new RedirectedUrl( "https://idp/Oidc.Azure", true ) );

            poco.Create<IWeakLoginCommand>().CrisPocoModel.HasSensitiveData.ShouldBeFalse();
            poco.Create<IGetLoginSchemesQCommand>().ShouldNotBeAssignableTo<ICommandEndpointOnly>();
            ((IList<string>)(await executor.RawExecuteAsync( services, poco.Create<IGetLoginSchemesQCommand>() )).Result!).ShouldBe( ["Basic", "Weak"] );

            // Impersonation requires the Critical level: this Normal session is refused.
            var receiver = services.GetRequiredService<RawCrisReceiver>();
            var impersonate = poco.Create<IImpersonateCommand>( c => { c.ActorId = 3712; c.TargetUserId = 12; } );
            var v = await receiver.IncomingValidateAsync( TestHelper.Monitor, services, impersonate );
            v.Success.ShouldBeFalse();
            v.ValidationMessages.Select( m => m.Text ).ShouldContain( "Invalid authentication level: Critical authentication level required." );
        }
    }
}
