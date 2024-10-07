using CK.Core;
using FluentAssertions;
using System.Net.Http;
using System.Threading.Tasks;

namespace CK.Cris.AspNet.Tests;

// Temporary: this is not the right pattern as it is NOT modular enough.
// Goal is more like the minimal API (linear configuration).
static class LocalHelper
{
    public const string CrisUri = "/.cris/net";

    //// Use the HttpCrisSender endpoint that allows "AllExchangeable" Poco. The /.cris is bound to
    //// a TypeFilterName that is or starts with "TypeScript" and this one is registered by CK.StObj.TypeScript.Engine
    //// that is not used here.
    //public const string CrisUri = "/.cris/net";
    //public const string BasicLoginUri = "/.webfront/c/basicLogin";
    //public const string LogoutUri = "/.webfront/c/logout";

    //public CrisTestHostServer( BinPathConfiguration binPath,
    //                           bool withAuthentication = false,
    //                           Action<IServiceCollection>? configureServices = null,
    //                           Action<IApplicationBuilder>? configureApplication = null )
    //{
    //    Throw.DebugAssert( binPath.Owner != null );
    //    binPath.Types.Add( typeof( CrisAspNetService ) );

    //    if( withAuthentication )
    //    {
    //        binPath.Types.Add( typeof( StdAuthenticationTypeSystem ),
    //                           typeof( AuthenticationInfoTokenService ),
    //                           typeof( FakeUserDatabase ),
    //                           typeof( FakeWebFrontLoginService ),
    //                           typeof( CrisAuthenticationService ),
    //                           typeof( CrisCultureService ),
    //                           typeof( IAuthAmbientValues ),
    //                           typeof( ICommandAuthUnsafe ),
    //                           typeof( ICommandAuthNormal ),
    //                           typeof( ICommandAuthCritical ),
    //                           typeof( ICommandAuthDeviceId ),
    //                           typeof( ICommandAuthImpersonation ) );
    //    }

    //    var map = binPath.Owner.RunSuccessfully().FirstBinPath.LoadMap();

    //    PocoDirectory = map.StObjs.Obtain<PocoDirectory>()!;

    //    var b = CK.AspNet.Tester.WebHostBuilderFactory.Create( null, null,
    //        services =>
    //        {
    //            // Don't UseCKMonitoring here or the GrandOutput.Default will be reconfigured:
    //            // only register the IActivityMonitor and its ParallelLogger.
    //            services.AddScoped<IActivityMonitor, ActivityMonitor>();
    //            services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );

    //            services.AddOptions();
    //            if( withAuthentication )
    //            {
    //                // Uses RootPath for cookies: no need to set the Token header, the TestServerClient cookie container
    //                // does the job.
    //                services.AddAuthentication().AddWebFrontAuth( options => options.CookieMode = AuthenticationCookieMode.RootPath );
    //            }
    //            services.AddStObjMap( TestHelper.Monitor, map );
    //            configureServices?.Invoke( services );
    //        },
    //        app =>
    //        {
    //            app.UseGuardRequestMonitor();
    //            if( withAuthentication )
    //            {
    //                app.UseAuthentication();
    //            }
    //            app.UseCris();
    //            configureApplication?.Invoke( app );
    //        },
    //        webBuilder =>
    //        {
    //            webBuilder.UseScopedHttpContext();
    //        }
    //    );

    //    var host = b.Build();
    //    host.Start();
    //    Client = new TestServerClient( host );
    //}

    static public async Task<IAspNetCrisResult> GetCrisResultAsync( this PocoDirectory p, HttpResponseMessage r )
    {
        var result = p.Find<IAspNetCrisResult>()!.ReadJson( await r.Content.ReadAsByteArrayAsync() );
        Throw.DebugAssert( result != null );
        return result;
    }

    static public async Task<IAspNetCrisResult> GetCrisResultWithCorrelationIdSetToNullAsync( this PocoDirectory p, HttpResponseMessage r )
    {
        var result = await GetCrisResultAsync( p, r );
        result.CorrelationId.Should().NotBeNullOrWhiteSpace();
        result.CorrelationId = null;
        return result;
    }

}
