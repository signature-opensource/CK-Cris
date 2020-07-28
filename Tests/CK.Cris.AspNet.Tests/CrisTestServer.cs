using CK.AspNet.Auth;
using CK.AspNet.Tester;
using CK.Auth;
using CK.Core;
using CK.Setup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.AspNet.Tests
{
    class CrisTestServer : IDisposable
    {
        public const string CrisUri = "/.cris";
        public const string BasicLoginUri = "/.webfront/c/basicLogin";
        public const string LogoutUri = "/.webfront/c/logout";

        public CrisTestServer(
            StObjCollector collector,
            Action<IServiceCollection>? configureServices = null,
            Action<IApplicationBuilder>? configureApplication = null )
        {
            collector.RegisterTypes( new[] {
                typeof( FrontCommandExecutor ),
                typeof( DefaultFrontCommandExceptionHandler ),
                typeof( CommandDirectory ),
                typeof( ICommandResult ),
                typeof( CommandValidator ),
                typeof( PocoJsonSerializer ),
                typeof( CrisAspNetService ),
                typeof( StdAuthenticationTypeSystem ),
                typeof( FakeWebFrontLoginService )
            } );

            var (result, stObjMap) = TestHelper.CompileAndLoadStObjMap( collector );

            var b = CK.AspNet.Tester.WebHostBuilderFactory.Create( null, null,
                services =>
                {
                    services.AddOptions();
                    services.AddAuthentication().AddWebFrontAuth( options => options.CookieMode = AuthenticationCookieMode.RootPath );
                    services.AddStObjMap( TestHelper.Monitor, stObjMap );
                    configureServices?.Invoke( services );
                },
                app =>
                {
                    app.UseGuardRequestMonitor();
                    app.UseCris();
                    configureApplication?.Invoke( app );
                },
                webBuilder =>
                {
                    webBuilder.UseScopedHttpContext();
                }
            ).UseMonitoring();

            var host = b.Build();
            host.Start();
            Client = new TestServerClient( host );
            Server = Client.Server;
        }

        public async Task<bool> Login( string userName, string password = "success" )
        {
            var body = $"{{\"userName\":\"{userName}\",\"password\":\"{password}\"}}";
            HttpResponseMessage response = await Client.PostJSON( BasicLoginUri, body );
            return response.IsSuccessStatusCode;
        }

        public async Task Logout() => await Client.Get( LogoutUri + "?full" );

        public TestServer Server { get; }

        public TestServerClient Client { get; }

        public void Dispose() => Server?.Dispose();

    }
}
