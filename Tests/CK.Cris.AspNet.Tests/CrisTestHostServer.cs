using CK.AspNet.Auth;
using CK.AspNet.Tester;
using CK.Auth;
using CK.Core;
using CK.Setup;
using CK.Testing;
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
    class CrisTestHostServer : IDisposable
    {
        // Use the HttpCrisSender endpoint that allows "AllExchangeable" Poco. The /.cris is bound to
        // a TypeFilterName that is or starts with "TypeScript" and this one is registered by CK.StObj.TypeScript.Engine
        // that is not used here.
        public const string CrisUri = "/.cris/net";
        public const string BasicLoginUri = "/.webfront/c/basicLogin";
        public const string LogoutUri = "/.webfront/c/logout";

        public CrisTestHostServer( StObjCollector collector,
                                   bool withAuthentication = false,
                                   Action<IServiceCollection>? configureServices = null,
                                   Action<IApplicationBuilder>? configureApplication = null )
        {
            collector.RegisterType( TestHelper.Monitor, typeof( CrisAspNetService ) );

            if( withAuthentication )
            {
                collector.RegisterTypes( TestHelper.Monitor, new[] {
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
                    // Don't UseCKMonitoring here or the GrandOutput.Default will be reconfigured:
                    // only register the IActivityMonitor and its ParallelLogger.
                    services.AddScoped<IActivityMonitor, ActivityMonitor>();
                    services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );

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
            );

            var host = b.Build();
            host.Start();
            Client = new TestServerClient( host );
        }

        public TestServerClient Client { get; }

        public PocoDirectory PocoDirectory { get; }

        public async Task<IAspNetCrisResult> GetCrisResultAsync( HttpResponseMessage r )
        {
            r.EnsureSuccessStatusCode();
            var result = PocoDirectory.Find<IAspNetCrisResult>()!.ReadJson( await r.Content.ReadAsByteArrayAsync() );
            Throw.DebugAssert( result != null );
            return result;
        }

        public async Task<IAspNetCrisResult> GetCrisResultWithCorrelationIdSetToNullAsync( HttpResponseMessage r )
        {
            var result = await GetCrisResultAsync( r );
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
