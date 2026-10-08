using CK.Auth;
using CK.Core;
using CK.IO.Authentication;
using CK.IO.Authentication.Basic;
using CK.IO.Authentication.Weak;
using CK.IO.Authentication.WeakZone;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

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

        [CommandHandler] public ISessionResult Handle( IBasicLoginCommand c, AuthenticationInfo info ) => Result( c, info );
        [CommandHandler] public RedirectedUrl Handle( IStartLoginCommand c ) => new RedirectedUrl( "https://idp/" + c.Scheme, c.InlineMode );
        [CommandHandler] public ISessionResult Handle( IRefreshSessionCommand c, AuthenticationInfo info ) => Result( c, info );
        [CommandHandler] public void Handle( ILogoutSessionCommand c ) { }
        [CommandHandler] public ISessionResult Handle( IImpersonateCommand c, AuthenticationInfo info ) => Result( c, info );
        [CommandHandler] public ISessionResult Handle( IClearImpersonationCommand c, AuthenticationInfo info ) => Result( c, info );
        [CommandHandler] public IList<string> Handle( IGetLoginSchemesQCommand c ) => ["Basic", "Weak"];
    }

    /// <summary>
    /// The authentication center side: a fake that opens a session for any scheme and tells what it received.
    /// </summary>
    public sealed class FakeSessionOpener : IAuthenticationSessionOpener
    {
        public ValueTask<ISessionResult> OpenSessionAsync<T>( IActivityMonitor monitor, T command, string scheme, object payload, bool rememberMe = false )
            where T : ICommand<ISessionResult>, ICommandAuthenticationSession
        {
            var weak = (IWeakLoginCommand)payload;
            var user = new UserInfo( 42, weak.WeakActorName, AuthLevel.Unsafe, [new UserSchemeInfo( scheme, DateTime.UtcNow )] );
            var info = AuthenticationInfo.Create( user, DateTime.UtcNow.AddHours( 1 ), null, "D", DateTime.UtcNow );
            return ValueTask.FromResult( command.CreateResult( r =>
            {
                r.Info.InitializeFrom( info );
                r.Token = "opened";
                r.RememberMe = rememberMe;
            } ) );
        }
    }

    /// <summary>
    /// The provider side: "I implement an authentication provider, I handle its login commands".
    /// This one is ignored here: IWeakZoneLoginCommand extends the command and a handler must handle the whole command.
    /// </summary>
    public sealed class FakeWeakProvider : ISingletonAutoService
    {
        [CommandHandler]
        public ValueTask<ISessionResult> HandleAsync( IActivityMonitor monitor, IWeakLoginCommand cmd, IAuthenticationSessionOpener opener )
            => throw new System.Diagnostics.UnreachableException();
    }

    /// <summary>
    /// The extension of the provider handles the extended command.
    /// </summary>
    public sealed class FakeWeakZoneProvider : ISingletonAutoService
    {
        [CommandHandler]
        public ValueTask<ISessionResult> HandleAsync( IActivityMonitor monitor, IWeakZoneLoginCommand cmd, IAuthenticationSessionOpener opener )
            => opener.OpenSessionAsync( monitor, cmd, "Weak", cmd );
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
                                              typeof( IBasicLoginCommand ),
                                              typeof( IStartLoginCommand ),
                                              typeof( IWeakLoginCommand ),
                                              typeof( IWeakZoneLoginCommand ),
                                              typeof( IRefreshSessionCommand ),
                                              typeof( ILogoutSessionCommand ),
                                              typeof( IImpersonateCommand ),
                                              typeof( IClearImpersonationCommand ),
                                              typeof( IGetLoginSchemesQCommand ),
                                              typeof( ISessionResult ),
                                              typeof( IAuthenticationInfoPoco ),
                                              typeof( IUserInfoPoco ),
                                              typeof( FakeAuthenticationCenter ),
                                              typeof( IAuthenticationSessionOpener ),
                                              typeof( FakeSessionOpener ),
                                              typeof( FakeWeakProvider ),
                                              typeof( FakeWeakZoneProvider ),
                                              typeof( IAuthenticationSessionEndpoint ) );
        var albert = new UserInfo( 3712, "Albert", AuthLevel.Critical, [new UserSchemeInfo( "Basic", new DateTime( 2000, 1, 1, 0, 0, 0, DateTimeKind.Utc ) )] );
        var session = AuthenticationInfo.Create( albert, DateTime.UtcNow.AddDays( 1 ), null, "D", DateTime.UtcNow );
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices( configureServices: services =>
            {
                services.AddScoped( s => session );
                services.AddScoped<IActivityMonitor>( s => TestHelper.Monitor );
            } );
            texts.ShouldNotContain( t => t.Contains( "is checked by no [IncomingValidator]" ), "The CrisAuthenticationService validates the parts." );

            using var scope = auto.Services.CreateScope();
            var services = scope.ServiceProvider;
            var poco = services.GetRequiredService<PocoDirectory>();
            var executor = services.GetRequiredService<RawCrisExecutor>();

            var login = poco.Create<IBasicLoginCommand>( c => { c.UserName = "Albert"; c.Password = "secret-password"; } );
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

            var weak = poco.Create<IWeakLoginCommand>( c => c.WeakActorName = "Operator" );
            weak.CrisPocoModel.HasSensitiveData.ShouldBeFalse();
            // CK.IO.Authentication.WeakZone adds the ZoneId to the weak login.
            weak.ShouldBeAssignableTo<IWeakZoneLoginCommand>().ZoneId = 42;
            ((IWeakZoneLoginCommand)weak).ZoneId.ShouldBe( 42 );
            // The provider handler of the whole (extended) command delegates to the session opener.
            var w = (ISessionResult)(await executor.RawExecuteAsync( services, weak )).Result!;
            w.Token.ShouldBe( "opened" );
            w.Info.UnsafeUser.UserName.ShouldBe( "Operator" );
            w.Info.UnsafeUser.Level.ShouldBe( AuthLevel.Unsafe );
            w.Info.UnsafeUser.Schemes.ShouldHaveSingleItem().Scheme.ShouldBe( "Weak" );
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
