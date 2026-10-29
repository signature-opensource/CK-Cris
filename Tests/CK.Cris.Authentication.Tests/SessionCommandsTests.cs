using CK.Auth;
using CK.Core;
using CK.IO.Authentication;
using CK.IO.Authentication.Basic;
using CK.IO.Authentication.Passkey;
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
/// First engine check of the CK.IO.Authentication types (they cannot be tested in CK-Cris-Abstractions) and of the
/// endpoint side of the session commands: the real handlers (CK.Cris.Authentication, .Basic, .Weak, .Passkey) execute
/// the internal operations, handled here (single host) by the fake authentication center of CK.Testing.Cris.Authentication.
/// </summary>
[TestFixture]
public class SessionCommandsTests
{
    /// <summary>
    /// The endpoint: it prepares numbered secrets and records what it is asked to issue or clear.
    /// </summary>
    public sealed class FakeEndpoint : IAuthenticationSessionEndpoint
    {
        int _prepared;

        public AuthenticationSessionProof? PresentedProof { get; set; }
        public string? PresentedToken { get; set; }
        public string? IssuedSessionKey { get; private set; }
        public bool Cleared { get; private set; }

        public (string RefreshSecretHash, string BindingSecretHash) PrepareSecrets()
        {
            ++_prepared;
            return ($"r{_prepared}", $"b{_prepared}");
        }

        public AuthenticationSessionProof Issue( string sessionKey, bool rememberMe, DateTime refreshExpires )
        {
            IssuedSessionKey = sessionKey;
            Cleared = false;
            PresentedProof = new AuthenticationSessionProof( sessionKey, $"r{_prepared}", $"b{_prepared}" );
            return PresentedProof;
        }

        public void Clear()
        {
            IssuedSessionKey = null;
            PresentedProof = null;
            Cleared = true;
        }

        public string? RemoteLoginCallbackUrl { get; set; } = "https://app/.cris/auth/callback";
        public string PrepareRemoteLogin() => "correlation-hash";
        public string? PresentedRemoteLoginHash { get; set; } = "presented-correlation";
        public string? WebOrigin { get; set; } = "https://app";
    }

    static readonly Type[] _sessionTypes =
    [
        typeof( CrisAuthenticationService ),
        typeof( AuthenticationInfo ),
        typeof( AuthenticationInfoDefaultProvider ),
        typeof( FakeActorDatabase ),
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
        typeof( IRemoteLoginCallbackCommand ),
        typeof( IGetLoginSchemesQCommand ),
        typeof( ILoginSchemeInfo ),
        typeof( IStartPasskeyLoginCommand ),
        typeof( IPasskeyLoginCommand ),
        typeof( IStartPasskeyRegistrationCommand ),
        typeof( IPasskeyRegistrationCommand ),
        typeof( IPasskeyRegistrationResult ),
        typeof( LoginUserDataValidator ),
        typeof( ISessionResult ),
        typeof( IAuthenticationInfoPoco ),
        typeof( IActorInfoPoco ),
        typeof( IAuthenticationSessionEndpoint ),
        typeof( IAuthenticationSessionOpener ),
        // The endpoint side.
        typeof( AuthenticationSessionEndpointService ),
        typeof( AuthenticationSessionHandlers ),
        typeof( BasicLoginHandler ),
        typeof( WeakLoginHandler ),
        typeof( PasskeyHandlers ),
        // The internal operations.
        typeof( ISessionOperationResult ),
        typeof( IOpenSessionOperationCommand ),
        typeof( IRefreshSessionOperationCommand ),
        typeof( ILogoutSessionOperationCommand ),
        typeof( IImpersonateOperationCommand ),
        typeof( IClearImpersonationOperationCommand ),
        typeof( IStartRemoteLoginOperationCommand ),
        typeof( ICompleteRemoteLoginOperationCommand ),
        typeof( IStartPasskeyLoginOperationCommand ),
        typeof( IFinishPasskeyLoginOperationCommand ),
        typeof( IStartPasskeyRegistrationOperationCommand ),
        typeof( IFinishPasskeyRegistrationOperationCommand )
    ];

    static readonly ActorInfo _albert = new ActorInfo( 3712, "Albert", CK.Core.AuthLevel.Critical, [new SchemeInfo( "Basic", new DateTime( 2000, 1, 1, 0, 0, 0, DateTimeKind.Utc ) )] );

    static async Task<AutomaticServices> CreateAsync( FakeEndpoint endpoint, bool withAuthenticationCenter )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( _sessionTypes );
        // The authentication center side: the handlers of the internal operations (CK.Testing.Cris.Authentication).
        if( withAuthenticationCenter ) configuration.FirstBinPath.AddFakeAuthCenter();
        var session = AuthenticationInfo.Create( _albert, DateTime.UtcNow.AddDays( 1 ), null, "D", DateTime.UtcNow );
        return (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices( configureServices: services =>
        {
            services.AddScoped( s => session );
            services.AddScoped<IActivityMonitor>( s => TestHelper.Monitor );
            services.AddScoped<IAuthenticationSessionEndpoint>( s => endpoint );
        } );
    }

    [Test]
    public async Task the_session_commands_are_executed_by_the_endpoint_with_the_operations_Async()
    {
        var endpoint = new FakeEndpoint();
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            await using var auto = await CreateAsync( endpoint, withAuthenticationCenter: true );
            texts.ShouldNotContain( t => t.Contains( "is checked by no [IncomingValidator]" ), "The CrisAuthenticationService validates the parts." );

            using var scope = auto.Services.CreateScope();
            var services = scope.ServiceProvider;
            var poco = services.GetRequiredService<PocoDirectory>();
            var executor = services.GetRequiredService<RawCrisExecutor>();
            var center = services.GetRequiredService<FakeAuthCenterOperations>();

            // The operations are [Internal]: front applications cannot send them.
            poco.Create<IOpenSessionOperationCommand>().CrisPocoModel.Exposure.ShouldBe( CrisExposure.Internal );
            poco.Create<IFinishPasskeyRegistrationOperationCommand>().CrisPocoModel.Exposure.ShouldBe( CrisExposure.Internal );
            poco.Create<IBasicLoginCommand>().CrisPocoModel.Exposure.ShouldBe( CrisExposure.Public );

            // Basic login: the login command is the payload of the open session operation (nested as data: the engine
            // accepts an endpoint-only command as a property), the endpoint issues the secrets it prepared.
            var login = poco.Create<IBasicLoginCommand>( c => { c.UserName = "Albert"; c.Password = FakeActorDatabase.BasicPassword; c.RememberMe = true; } );
            login.ShouldBeAssignableTo<ICommandEndpointOnly>();
            login.CrisPocoModel.HasSensitiveData.ShouldBeTrue();
            var r = (ISessionResult)(await executor.RawExecuteAsync( services, login )).Result!;
            r.Success.ShouldBeTrue();
            var token = r.Token.ShouldNotBeNull();
            r.RememberMe.ShouldBeTrue();
            r.Info.UnsafeActor.ActorId.ShouldBe( 3712 );
            r.Info.UnsafeActor.Level.ShouldBe( CK.Core.AuthLevel.Normal );
            r.Info.DeviceId.ShouldBe( center.DeviceId );
            center.LastOpen!.Scheme.ShouldBe( "Basic" );
            center.LastOpen.Payload.ShouldBeSameAs( login );
            center.LastOpen.RememberMe.ShouldBeTrue();
            center.LastOpen.NextRefreshSecretHash.ShouldBe( "r1" );
            center.LastOpen.NextBindingSecretHash.ShouldBe( "b1" );
            var sessionKey = endpoint.IssuedSessionKey.ShouldNotBeNull();

            // Refresh: the presented session travels with the operation, the new prepared secrets are issued.
            r = (ISessionResult)(await executor.RawExecuteAsync( services, poco.Create<IRefreshSessionCommand>() )).Result!;
            r.Token.ShouldNotBeNull().ShouldNotBe( token );
            center.LastRefresh!.PresentedSessionKey.ShouldBe( sessionKey );
            center.LastRefresh.PresentedRefreshSecretHash.ShouldBe( "r1" );
            center.LastRefresh.NextRefreshSecretHash.ShouldBe( "r2" );
            endpoint.PresentedProof!.RefreshSecretHash.ShouldBe( "r2" );
            sessionKey = endpoint.IssuedSessionKey.ShouldNotBeNull();

            // Impersonation: a Critical operation that carries the current actor.
            r = (ISessionResult)(await executor.RawExecuteAsync( services, poco.Create<IImpersonateCommand>( c => { c.ActorId = 3712; c.TargetActorId = 3713; } ) )).Result!;
            r.Info.UnsafeActor.ActorName.ShouldBe( "Robert" );
            r.Info.UnsafeActualActor.ActorName.ShouldBe( "Albert" );
            center.LastImpersonate!.ActorId.ShouldBe( 3712 );
            center.LastImpersonate.TargetActorId.ShouldBe( 3713 );
            center.LastImpersonate.PresentedSessionKey.ShouldBe( sessionKey );
            r = (ISessionResult)(await executor.RawExecuteAsync( services, poco.Create<IClearImpersonationCommand>() )).Result!;
            r.Info.UnsafeActor.ActorName.ShouldBe( "Albert" );

            // Remote login: the endpoint's callback, correlation and origin travel with the start; the presented
            // correlation with the completion.
            var start = poco.Create<IStartLoginCommand>( c => { c.Scheme = "Oidc.Azure"; c.InlineMode = true; c.UserData.Add( "InvitationToken", "T" ); } );
            start.CrisPocoModel.ResultType.ShouldBe( typeof( RedirectedUrl ) );
            var redirect = (RedirectedUrl)(await executor.RawExecuteAsync( services, start )).Result!;
            redirect.IsInline.ShouldBeTrue();
            redirect.Url.ShouldStartWith( "https://idp.example.com/authorize?state=" );
            center.LastStartRemote!.Login.ShouldBeSameAs( start );
            center.LastStartRemote.CallbackUrl.ShouldBe( "https://app/.cris/auth/callback" );
            center.LastStartRemote.CorrelationHash.ShouldBe( "correlation-hash" );
            center.LastStartRemote.WebOrigin.ShouldBe( "https://app" );
            var state = redirect.Url.Substring( redirect.Url.LastIndexOf( '=' ) + 1 );
            // The presented correlation is not the one of the start: refused.
            var callback = poco.Create<IRemoteLoginCallbackCommand>( c => { c.State = state; c.Code = FakeAuthCenterOperations.RemoteLoginCode; } );
            ((ISessionResult)(await executor.RawExecuteAsync( services, callback )).Result!).Success.ShouldBeFalse();
            center.LastCompleteRemote!.Callback.ShouldBeSameAs( callback );
            center.LastCompleteRemote.PresentedCorrelationHash.ShouldBe( "presented-correlation" );
            // From the browser that started it: accepted.
            redirect = (RedirectedUrl)(await executor.RawExecuteAsync( services, start )).Result!;
            state = redirect.Url.Substring( redirect.Url.LastIndexOf( '=' ) + 1 );
            endpoint.PresentedRemoteLoginHash = "correlation-hash";
            callback = poco.Create<IRemoteLoginCallbackCommand>( c => { c.State = state; c.Code = FakeAuthCenterOperations.RemoteLoginCode; } );
            ((ISessionResult)(await executor.RawExecuteAsync( services, callback )).Result!).Success.ShouldBeTrue();

            // Weak login: the Weak handler (AllowUnclosedCommand) handles the command extended by CK.IO.Authentication.WeakZone.
            var weak = poco.Create<IWeakLoginCommand>( c => c.WeakActorName = "Operator" );
            weak.CrisPocoModel.HasSensitiveData.ShouldBeFalse();
            weak.ShouldBeAssignableTo<IWeakZoneLoginCommand>().ZoneId = 42;
            var w = (ISessionResult)(await executor.RawExecuteAsync( services, weak )).Result!;
            w.Token.ShouldNotBeNull();
            w.Info.UnsafeActor.ActorName.ShouldBe( "Operator" );
            w.Info.UnsafeActor.Level.ShouldBe( CK.Core.AuthLevel.Unsafe );
            w.Info.UnsafeActor.Schemes.ShouldHaveSingleItem().Scheme.ShouldBe( "Weak" );
            center.LastOpen.Payload.ShouldBeAssignableTo<IWeakZoneLoginCommand>().ZoneId.ShouldBe( 42 );

            poco.Create<IGetLoginSchemesQCommand>().ShouldNotBeAssignableTo<ICommandEndpointOnly>();
            var schemes = (IList<ILoginSchemeInfo>)(await executor.RawExecuteAsync( services, poco.Create<IGetLoginSchemesQCommand>() )).Result!;
            schemes.Select( s => (s.Name, s.CriticalLifetime) ).ShouldBe( [("Basic", TimeSpan.FromMinutes( 5 )), ("Weak", TimeSpan.Zero), ("Passkey", TimeSpan.FromMinutes( 5 ))] );

            // Impersonation requires the Critical level: this Normal session is refused at the entry.
            var receiver = services.GetRequiredService<RawCrisReceiver>();
            var impersonate = poco.Create<IImpersonateCommand>( c => { c.ActorId = 3712; c.TargetActorId = 3713; } );
            var v = await receiver.IncomingValidateAsync( TestHelper.Monitor, services, impersonate );
            v.Success.ShouldBeFalse();
            v.ValidationMessages.Select( m => m.Text ).ShouldContain( "Invalid authentication level: Critical authentication level required." );
            v.ValidationMessages.Select( m => m.ResName ).ShouldContain( "Authentication.CriticalLevelRequired" );

            // Every login command has the user data part.
            login.ShouldBeAssignableTo<ICommandLoginUserData>();
            start.ShouldBeAssignableTo<ICommandLoginUserData>();
            weak.ShouldBeAssignableTo<ICommandLoginUserData>();
            poco.Create<IPasskeyLoginCommand>().ShouldBeAssignableTo<ICommandLoginUserData>();
            typeof( ICommandLoginUserData ).IsAssignableFrom( typeof( IRemoteLoginCallbackCommand ) )
                .ShouldBeFalse( "The data stay on the authentication center between the start and the callback." );

            // The passkey commands: session commands, nothing sensitive, the options as JSON strings.
            var startPasskey = poco.Create<IStartPasskeyLoginCommand>();
            startPasskey.ShouldBeAssignableTo<ICommandEndpointOnly>();
            startPasskey.CrisPocoModel.ResultType.ShouldBe( typeof( string ) );
            ((string)(await executor.RawExecuteAsync( services, startPasskey )).Result!).ShouldContain( "login-challenge" );
            var passkeyLogin = poco.Create<IPasskeyLoginCommand>( c => { c.Response = "{}"; c.RememberMe = true; } );
            passkeyLogin.CrisPocoModel.HasSensitiveData.ShouldBeFalse();
            ((ISessionResult)(await executor.RawExecuteAsync( services, passkeyLogin )).Result!).Success.ShouldBeFalse( "Not signed." );
            passkeyLogin.Response = """{"signed":"login-challenge"}""";
            r = (ISessionResult)(await executor.RawExecuteAsync( services, passkeyLogin )).Result!;
            r.Info.UnsafeActor.Level.ShouldBe( CK.Core.AuthLevel.Critical );

            // Adding a passkey requires the Critical level: this Normal session is refused at the entry.
            var startRegistration = poco.Create<IStartPasskeyRegistrationCommand>( c => { c.ActorId = 3712; c.Name = "My phone"; } );
            startRegistration.CrisPocoModel.ResultType.ShouldBe( typeof( string ) );
            v = await receiver.IncomingValidateAsync( TestHelper.Monitor, services, startRegistration );
            v.Success.ShouldBeFalse();
            v.ValidationMessages.Select( m => m.Text ).ShouldContain( "Invalid authentication level: Critical authentication level required." );
            v.ValidationMessages.Select( m => m.ResName ).ShouldContain( "Authentication.CriticalLevelRequired" );
            ((string)(await executor.RawExecuteAsync( services, startRegistration )).Result!).ShouldContain( "registration-challenge" );
            center.LastStartRegistration!.Start.ShouldBeSameAs( startRegistration );
            center.LastStartRegistration.ActorId.ShouldBe( 3712 );
            center.LastStartRegistration.WebOrigin.ShouldBe( "https://app" );
            var registration = poco.Create<IPasskeyRegistrationCommand>( c => { c.ActorId = 3712; c.Response = "{}"; } );
            v = await receiver.IncomingValidateAsync( TestHelper.Monitor, services, registration );
            v.Success.ShouldBeFalse();
            var registered = (IPasskeyRegistrationResult)(await executor.RawExecuteAsync( services, registration )).Result!;
            registered.Success.ShouldBeTrue();
            registered.Name.ShouldBe( "Test passkey" );
            center.LastFinishRegistration!.Registration.ShouldBeSameAs( registration );

            // A passkey ceremony needs an endpoint that serves web pages.
            endpoint.WebOrigin = null;
            (await executor.RawExecuteAsync( services, poco.Create<IStartPasskeyLoginCommand>() )).Result
                .ShouldBeAssignableTo<ICrisResultError>().Errors.ShouldContain( e => e.Text.Contains( "Passkeys require an endpoint that serves web pages." ) );

            // Logout: the endpoint is cleared.
            (await executor.RawExecuteAsync( services, poco.Create<ILogoutSessionCommand>() )).Result.ShouldBeNull();
            endpoint.Cleared.ShouldBeTrue();
        }
    }

    [Test]
    public async Task an_operation_that_cannot_be_executed_decides_nothing_Async()
    {
        // No authentication center here and no dispatcher: the operations have no handler.
        var endpoint = new FakeEndpoint { PresentedProof = new AuthenticationSessionProof( "session-key", "r0", "b0" ) };
        await using var auto = await CreateAsync( endpoint, withAuthenticationCenter: false );
        using var scope = auto.Services.CreateScope();
        var services = scope.ServiceProvider;
        var poco = services.GetRequiredService<PocoDirectory>();
        var executor = services.GetRequiredService<RawCrisExecutor>();

        // An error, not a failed ISessionResult: nothing has been decided, the client keeps its session and retries.
        var r = (await executor.RawExecuteAsync( services, poco.Create<IRefreshSessionCommand>() )).Result;
        r.ShouldBeAssignableTo<ICrisResultError>().Errors.ShouldContain( e => e.Text.Contains( "has no command handler" ) );
        endpoint.Cleared.ShouldBeFalse();
        endpoint.IssuedSessionKey.ShouldBeNull();

        // The logout clears the endpoint whatever happens.
        (await executor.RawExecuteAsync( services, poco.Create<ILogoutSessionCommand>() )).Result.ShouldBeAssignableTo<ICrisResultError>();
        endpoint.Cleared.ShouldBeTrue();
    }

    [Test]
    public async Task the_login_user_data_are_limited_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( LoginUserDataValidator ),
                                              typeof( RawCrisReceiver ),
                                              typeof( CrisDirectory ),
                                              typeof( ICrisResultError ),
                                              typeof( IStartLoginCommand ),
                                              typeof( IWeakLoginCommand ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices( configureServices: services =>
        {
            services.AddScoped<IActivityMonitor>( s => TestHelper.Monitor );
        } );
        using var scope = auto.Services.CreateScope();
        var services = scope.ServiceProvider;
        var poco = services.GetRequiredService<PocoDirectory>();
        var receiver = services.GetRequiredService<RawCrisReceiver>();

        async Task<bool> IsValidAsync( ICommandLoginUserData c ) => (await receiver.IncomingValidateAsync( TestHelper.Monitor, services, c )).Success;

        var start = poco.Create<IStartLoginCommand>( c => c.Scheme = "Oidc.Test" );
        (await IsValidAsync( start )).ShouldBeTrue();
        for( int i = 0; i < LoginUserDataValidator.MaxEntries; ++i ) start.UserData.Add( $"K{i}", "V" );
        (await IsValidAsync( start )).ShouldBeTrue( "16 entries is the maximum." );
        start.UserData.Add( "OneMore", "V" );
        (await IsValidAsync( start )).ShouldBeFalse();

        var weak = poco.Create<IWeakLoginCommand>( c => c.WeakActorName = "Operator" );
        weak.UserData.Add( "Token", new string( 'x', LoginUserDataValidator.MaxLength - "Token".Length ) );
        (await IsValidAsync( weak )).ShouldBeTrue( "4096 characters (keys and values) is the maximum." );
        weak.UserData["Token"] += "x";
        (await IsValidAsync( weak )).ShouldBeFalse();
    }
}
