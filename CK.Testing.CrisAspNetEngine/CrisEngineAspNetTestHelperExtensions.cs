using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using System.Collections.Generic;
using System;
using System.Linq;
using CK.Testing.StObjEngine;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CK.AspNet.Auth;
using CK.Auth;
using CK.Cris.AspNet;

namespace CK.Testing
{
    /// <summary>
    /// Extends <see cref="Testing.IStObjEngineTestHelper"/> to support end to end TypeScript tests.
    /// </summary>
    public static class CrisEngineAspNetTestHelperExtensions
    {
        /// <summary>
        /// Runs a test in "TSTest" folder thanks to a "yarn test" command with
        /// a "CRIS_ENDPOINT_URL" environment variable that is the address of a temporary server
        /// setup with Cris middleware.
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="targetProjectPath">Must be obtained by <see cref="StObjEngineTestHelperTypeScriptExtensions.GetTypeScriptWithTestsSupportTargetProjectPath(IBasicTestHelper, string?)"/>.</param>
        /// <param name="registeredTypes">The types to register in the <see cref="StObjCollector"/>.</param>
        /// <param name="tsTypes">The types that must be generated in TypeScript.</param>
        /// <param name="resume">
        /// Wait callback called when <see cref="Debugger.IsAttached"/> is true before running test.
        /// See <see cref="Monitoring.IMonitorTestHelperCore.SuspendAsync(Func{bool, bool}, string?, int, string?)"/>.
        /// </param>
        /// <param name="configureEngine">Optional engine configurator.</param>
        /// <param name="configureServices">Optional services configurator.</param>
        /// <param name="configureApplication">Optional application configurator.</param>
        /// <returns>The awaitable.</returns>
        public static Task RunSingleBinPathAspNetE2ETestAsync( this IMonitorTestHelper helper,
                                                               NormalizedPath targetProjectPath,
                                                               ISet<Type> registeredTypes,
                                                               IEnumerable<Type> tsTypes,
                                                               Func<bool, bool> resume,
                                                               Action<IServiceCollection>? configureServices = null,
                                                               Action<IApplicationBuilder>? configureApplication = null,
                                                               [CallerMemberName] string? testName = null,
                                                               [CallerLineNumber] int lineNumber = 0,
                                                               [CallerFilePath] string? fileName = null )
        {
            Throw.CheckNotNullArgument( resume );
            return RunSingleBinPathAspNetE2ETestAsync( helper,
                                                       helper.CreateDefaultEngineConfiguration(),    
                                                       targetProjectPath,
                                                       registeredTypes,
                                                       tsTypes,
                                                       resume,
                                                       configureServices,
                                                       configureApplication );
        }

        /// <summary>
        /// Runs a test in "TSTest" folder thanks to a "yarn test" command with
        /// a "CRIS_ENDPOINT_URL" environment variable that is the address of a temporary server
        /// setup with Cris middleware.
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="engineConfiguration">The engine configuration.</param>
        /// <param name="targetProjectPath">Must be obtained by <see cref="StObjEngineTestHelperTypeScriptExtensions.GetTypeScriptWithTestsSupportTargetProjectPath(IBasicTestHelper, string?)"/>.</param>
        /// <param name="registeredTypes">The types to register in the <see cref="StObjCollector"/>.</param>
        /// <param name="tsTypes">The types that must be generated in TypeScript.</param>
        /// <param name="resume">
        /// Wait callback called when <see cref="Debugger.IsAttached"/> is true before running test.
        /// See <see cref="Monitoring.IMonitorTestHelperCore.SuspendAsync(Func{bool, bool}, string?, int, string?)"/>.
        /// </param>
        /// <param name="configureEngine">Optional engine configurator.</param>
        /// <param name="configureServices">Optional services configurator.</param>
        /// <param name="configureApplication">Optional application configurator.</param>
        /// <returns>The awaitable.</returns>
        public static Task RunSingleBinPathAspNetE2ETestAsync( this IMonitorTestHelper helper,
                                                               EngineConfiguration engineConfiguration,
                                                               NormalizedPath targetProjectPath,
                                                               ISet<Type> registeredTypes,
                                                               IEnumerable<Type> tsTypes,
                                                               Func<bool, bool> resume,
                                                               Action<IServiceCollection>? configureServices = null,
                                                               Action<IApplicationBuilder>? configureApplication = null,
                                                               [CallerMemberName] string? testName = null,
                                                               [CallerLineNumber] int lineNumber = 0,
                                                               [CallerFilePath] string? fileName = null )
        {
            Throw.CheckNotNullArgument( resume );
            return RunSingleBinPathAspNetE2ETestAsync( helper,
                                                       engineConfiguration,    
                                                       targetProjectPath,
                                                       registeredTypes,
                                                       tsTypes,
                                                       beforeRun: runner => helper.SuspendAsync( resume, testName, lineNumber, fileName ),
                                                       configureServices,
                                                       configureApplication );
        }

        /// <summary>
        /// Runs a test in "TSTest" folder thanks to a "yarn test" command with
        /// a "CRIS_ENDPOINT_URL" environment variable that is the address of a temporary server
        /// setup with Cris middleware.
        /// <para>
        /// The server has CK.AspNet.Auth
        /// </para>
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="engineConfiguration">The engine configuration.</param>
        /// <param name="targetProjectPath">Must be obtained by <see cref="CK.StObjEngineTestHelperExtensions.GetTypeScriptWithTestsSupportTargetProjectPath(IBasicTestHelper, string?)"/>.</param>
        /// <param name="registeredTypes">The types to register in the <see cref="StObjCollector"/>.</param>
        /// <param name="beforeRun">
        /// Optional asynchronous action called right before tests run.
        /// Can be used to provide disposal actions and/or breakpoints or suspension.
        /// </param>
        /// <param name="tsTypes">The types that must be generated in TypeScript.</param>
        /// <param name="configureServices">Optional services configurator.</param>
        /// <param name="configureApplication">Optional application configurator.</param>
        /// <returns>The awaitable.</returns>
        public static async Task RunSingleBinPathAspNetE2ETestAsync( this IMonitorTestHelper helper,
                                                                     EngineConfiguration engineConfiguration,
                                                                     NormalizedPath targetProjectPath,
                                                                     ISet<Type> registeredTypes,
                                                                     IEnumerable<Type> tsTypes,
                                                                     Func<TypeScriptEngineTestHelperExtensions.Runner, Task>? beforeRun = null,
                                                                     Action<IServiceCollection>? configureServices = null,
                                                                     Action<IApplicationBuilder>? configureApplication = null )
        {
            // These 2 services are required by the WebFrontAuthService.
            registeredTypes.Add( typeof( AuthenticationInfoTokenService ) );
            registeredTypes.Add( typeof( StdAuthenticationTypeSystem ) );
            registeredTypes.Add( typeof( FakeUserDatabase ) );
            registeredTypes.Add( typeof( FakeWebFrontLoginService ) );
            registeredTypes.Add( typeof( CrisAspNetService ) );

            helper.EnsureTypeScriptConfigurationAspect( engineConfiguration, targetProjectPath, tsTypes.ToArray() );
            var map = helper.RunSingleBinPathAndLoad( engineConfiguration, registeredTypes ).Map;
            await using var server = await CreateAspNetServerAsync( helper, map, configureServices, configureApplication );
            var endpointUrl = server.ServerAddress + "/.cris";
            helper.Monitor.Info( $"Server started. Cris endpoint url: '{endpointUrl}'." );

            // We set the CRIS_ENDPOINT_URL environment variable: the tests can use it.
            await using var runner = helper.CreateTypeScriptRunner( targetProjectPath, new Dictionary<string, string> { { "CRIS_ENDPOINT_URL", endpointUrl } } );
            if( beforeRun != null )
            {
                await beforeRun.Invoke( runner );
            }
            runner.Run();
        }

        public static Task<RunningAspNetServer> CreateSingleBinPathAspNetServerAsync( this IMonitorTestHelper helper,
                                                                                      ISet<Type> types,
                                                                                      Action<IServiceCollection>? configureServices = null,
                                                                                      Action<IApplicationBuilder>? configureApplication = null )
        {
            var map = helper.RunSingleBinPathAndLoad( types ).Map;
            return CreateAspNetServerAsync( helper, map, configureServices, configureApplication ); 
        }

        /// <summary>
        /// Creates, configure and starts a <see cref="RunningAspNetServer"/>.
        /// <para>
        /// This server is configured with <see cref="WebFrontAuthService"/>.
        /// </para>
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="map">The StObjMap.</param>
        /// <param name="configureServices">Optional application services configurator.</param>
        /// <param name="configureApplication">Optional application configurator.</param>
        /// <returns>A running .NET server.</returns>
        public static async Task<RunningAspNetServer> CreateAspNetServerAsync( IMonitorTestHelper helper,
                                                                               IStObjMap map,
                                                                               Action<IServiceCollection>? configureServices,
                                                                               Action<IApplicationBuilder>? configureApplication )
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseScopedHttpContext();
            // Don't UseCKMonitoring here or the GrandOutput.Default will be reconfigured:
            // only register the IActivityMonitor and its ParallelLogger.
            builder.Services.AddScoped<IActivityMonitor, ActivityMonitor>();
            builder.Services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );

            builder.Services.AddAuthentication( WebFrontAuthOptions.OnlyAuthenticationScheme )
                        .AddWebFrontAuth();

            configureServices?.Invoke( builder.Services );

            builder.Services.AddStObjMap( helper.Monitor, map );

            var app = builder.Build();
            try
            {
                // This chooses a random, free port.
                app.Urls.Add( "http://[::1]:0" );

                app.UseGuardRequestMonitor();
                app.UseAuthentication();
                app.UseCris();
                configureApplication?.Invoke( app );

                await app.StartAsync();

                // The IServer's IServerAddressesFeature feature has the address resolved.
                var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
                var addresses = server.Features.Get<IServerAddressesFeature>();
                Throw.DebugAssert( addresses != null && addresses.Addresses.Count > 0 );

                var serverAddress = addresses.Addresses.First();
                helper.Monitor.Info( $"Server started. Server address: '{serverAddress}'." );
                return new RunningAspNetServer( app, serverAddress );
            }
            catch( Exception ex )
            {
                helper.Monitor.Error( "Unhandled error while starting http server.", ex );
                await app.DisposeAsync();
                throw;
            }
        }
    }

}
