using CK.AspNet.Auth;
using CK.AspNet.Tester;
using CK.Auth;
using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.AspNet.Tests
{
    class CrisTestServer : IDisposable
    {
        public const string CrisUri = "/.cris";
        public const string BasicLoginUri = "/.webfront/c/basicLogin";
        public const string LogoutUri = "/.webfront/c/logout";

        public CrisTestServer( StObjCollector collector,
                               bool withAuthentication = false,
                               Action<IServiceCollection>? configureServices = null,
                               Action<IApplicationBuilder>? configureApplication = null )
        {
            collector.RegisterTypes( new[] {
                typeof( RawCrisExecutor ),
                typeof( RawCrisValidator ),
                typeof( DefaultFrontCommandExceptionHandler ),
                typeof( ICrisResult ),
                typeof( ICrisResultError ),
                typeof( PocoJsonSerializer ),
                typeof( CrisAspNetService ),
                typeof( AmbientValues.IAmbientValues ),
                typeof( AmbientValues.IAmbientValuesCollectCommand ),
                typeof( AmbientValues.AmbientValuesService )
            } );

            if( withAuthentication )
            {
                collector.RegisterTypes( new[] {
                    typeof( StdAuthenticationTypeSystem ),
                    typeof( FakeWebFrontLoginService ),
                    typeof( CrisAuthenticationService ),
                    typeof( AuthenticationInfoTokenService ),
                    typeof( IAuthAmbientValues ),
                    typeof( ICommandAuthUnsafe ),
                    typeof( ICommandAuthNormal ),
                    typeof( ICommandAuthCritical ),
                    typeof( ICommandAuthDeviceId ),
                    typeof( ICommandAuthImpersonation )
            } );
            }

            var (result, stObjMap) = TestHelper.CompileAndLoadStObjMap( collector );

            PocoDirectory = stObjMap.StObjs.Obtain<PocoDirectory>()!;

            var b = CK.AspNet.Tester.WebHostBuilderFactory.Create( null, null,
                services =>
                {
                    services.AddOptions();
                    if( withAuthentication )
                    {
                        // Uses RootPath for cookies: no need to set the Token header, the TestServerClient cookie container
                        // does the job.
                        services.AddAuthentication().AddWebFrontAuth( options => options.CookieMode = AuthenticationCookieMode.RootPath );
                    }
                    services.AddStObjMap( TestHelper.Monitor, stObjMap );
                    configureServices?.Invoke( services );
                },
                app =>
                {
                    app.UseGuardRequestMonitor();
                    if( withAuthentication )
                    {
                        app.UseAuthentication();
                    }
                    app.UseCris();
                    configureApplication?.Invoke( app );
                },
                webBuilder =>
                {
                    webBuilder.UseScopedHttpContext();
                }
            ).UseCKMonitoring();

            var host = b.Build();
            host.Start();
            Client = new TestServerClient( host );
        }

        public TestServerClient Client { get; }

        public PocoDirectory PocoDirectory { get; }

        public async Task<ICrisResult> GetCrisResultWithNullCorrelationIdAsync( HttpResponseMessage r )
        {
            r.EnsureSuccessStatusCode();
            var result = PocoDirectory.Find<ICrisResult>()!.JsonDeserialize( await r.Content.ReadAsStringAsync() );
            Debug.Assert( result != null );
            result.CorrelationId.Should().NotBeNullOrWhiteSpace();
            result.CorrelationId = null;
            return result;
        }

        public async Task<bool> LoginAsync( string userName, string password = "success" )
        {
            var body = $"{{\"userName\":\"{userName}\",\"password\":\"{password}\"}}";
            HttpResponseMessage response = await Client.PostJSONAsync( BasicLoginUri, body );
            return response.IsSuccessStatusCode;
        }

        public Task LogoutAsync() => Client.GetAsync( LogoutUri + "?full" );

        public void Dispose()
        {
            //Client.Dispose(); // CK.Monitoring.Hosting is bugged and dispose GrandOutput.Default.
        }
    }
}
