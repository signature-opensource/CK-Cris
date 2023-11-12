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
using FluentAssertions.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CK.AspNet.Auth;
using CK.Auth;

namespace CK
{
    /// <summary>
    /// Extends <see cref="Testing.IStObjEngineTestHelper"/> to support end to end TypeScript tests.
    /// </summary>
    public static class StObjEngineTestHelperAspNetCrisExtensions
    {
        private sealed class MonoCollectorResolver : IStObjCollectorResultResolver
        {
            private readonly IStObjEngineTestHelper _helper;

            private readonly Type[] _types;

            public MonoCollectorResolver( IStObjEngineTestHelper helper, params Type[] types )
            {
                _helper = helper;
                _types = types;
            }

            public StObjCollectorResult? GetResult( RunningBinPathGroup g )
            {
                return _helper.GetSuccessfulResult( _helper.CreateStObjCollector( _types ) );
            }
        }

        /// <summary>
        /// Runs a test in "TSTest" folder thanks to a "yarn test" command with
        /// a "CRIS_ENDPOINT_URL" environment variable that is the address of a temporary server
        /// setup with Cris middleware.
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="targetProjectPath">Must be obtained by <see cref="CK.StObjEngineTestHelperExtensions.GetTypeScriptWithTestsSupportTargetProjectPath(IBasicTestHelper, string?)"/>.</param>
        /// <param name="registeredTypes">The types to register in the <see cref="StObjCollector"/>.</param>
        /// <param name="tsTypes">The types that must be generated in TypeScript.</param>
        /// <param name="resume">
        /// Wait callback called when <see cref="Debugger.IsAttached"/> is true before running test.
        /// See <see cref="StObjEngineTestHelperTypeScriptExtensions.SuspendAsync(IMonitorTestHelper, Func{bool, bool}, string?, int, string?)"/>.
        /// </param>
        /// <param name="configureServices">Optional services configurator.</param>
        /// <param name="configureApplication">Optional application configurator.</param>
        /// <returns>The awaitable.</returns>
        public static Task RunAspNetE2ETestAsync( this IStObjEngineTestHelper helper,
                                                  NormalizedPath targetProjectPath,
                                                  IEnumerable<Type> registeredTypes,
                                                  IEnumerable<Type> tsTypes,
                                                  Func<bool, bool> resume,
                                                  Action<IServiceCollection>? configureServices = null,
                                                  Action<IApplicationBuilder>? configureApplication = null,
                                                  [CallerMemberName] string? testName = null,
                                                  [CallerLineNumber] int lineNumber = 0,
                                                  [CallerFilePath] string? fileName = null )
        {
            Throw.CheckNotNullArgument( resume );
            return RunAspNetE2ETestAsync( helper,
                                          targetProjectPath,
                                          registeredTypes,
                                          tsTypes,
                                          runner => helper.SuspendAsync( resume, testName, lineNumber, fileName ),
                                          configureServices,
                                          configureApplication );
        }

        /// <summary>
        /// Runs a test in "TSTest" folder thanks to a "yarn test" command with
        /// a "CRIS_ENDPOINT_URL" environment variable that is the address of a temporary server
        /// setup with Cris middleware.
        /// </summary>
        /// <param name="helper">This helper.</param>
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
        public static async Task RunAspNetE2ETestAsync( this IStObjEngineTestHelper helper,
                                                        NormalizedPath targetProjectPath,
                                                        IEnumerable<Type> registeredTypes,
                                                        IEnumerable<Type> tsTypes,
                                                        Func<StObjEngineTestHelperTypeScriptExtensions.TypeScriptRunner, Task>? beforeRun = null,
                                                        Action<IServiceCollection>? configureServices = null,
                                                        Action<IApplicationBuilder>? configureApplication = null )
        {
            var config = helper.ConfigureTypeScript( null, targetProjectPath, tsTypes.ToArray() );
            config.BinPaths[0].CompileOption = CompileOption.Compile;

            StObjEngine stObjEngine = new StObjEngine( helper.Monitor, config );
            MonoCollectorResolver resolver = new MonoCollectorResolver( helper, registeredTypes.ToArray() );
            var runResult = stObjEngine.Run( resolver );
            runResult.Success.Should().BeTrue( "StObjEngine.Run worked." );
            var stobjMap = runResult.Groups[0].LoadStObjMap( helper.Monitor );

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseScopedHttpContext();
            // Don't UseCKMonitoring here or the GrandOutput.Default will be reconfigured:
            // only register the IActivityMonitor and its ParallelLogger.
            builder.Services.AddScoped<IActivityMonitor, ActivityMonitor>();
            builder.Services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );

            configureServices?.Invoke( builder.Services );
            builder.Services.AddStObjMap( helper.Monitor, stobjMap );

            var app = builder.Build();
            try
            {
                // This chooses a random, free port.
                app.Urls.Add( "http://[::1]:0" );

                app.UseGuardRequestMonitor();
                app.UseCris();
                configureApplication?.Invoke( app );

                using( helper.Monitor.OpenInfo( "Starting server and running TS tests." ) )
                {
                    await app.StartAsync();

                    // The IServer's IServerAddressesFeature feature has the address resolved.
                    var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
                    var addresses = server.Features.Get<IServerAddressesFeature>();
                    Throw.DebugAssert( addresses != null && addresses.Addresses.Count > 0 );

                    var endpointUrl = addresses.Addresses.First() + "/.cris";
                    helper.Monitor.Info( $"Server started. Cris endpoint url: '{endpointUrl}'." );
                    // We set the CRIS_ENDPOINT_URL environment variable: the tests can use it.
                    await using var runner = helper.CreateTypeScriptRunner( targetProjectPath, new Dictionary<string, string> { { "CRIS_ENDPOINT_URL", endpointUrl } } );
                    if( beforeRun != null )
                    {
                        await beforeRun.Invoke( runner );
                    }
                    runner.Run();
                    await app.StopAsync();
                }
            }
            catch( Exception ex )
            {
                helper.Monitor.Error( "Unhandled error while running http server.", ex );
                throw;
            }
            finally
            {
                await app.DisposeAsync();
            }
        }

        /// <summary>
        /// Creates, configure and starts a <see cref="RunningAspNetServer"/>.
        /// No types are implicitly registered: <paramref name="registeredTypes"/> should contain <see cref="CK.Cris.AspNet.CrisAspNetService"/>.
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="registeredTypes">The types to register in the StObjCollector.</param>
        /// <param name="engineConfigurator">Optional engine configurator.</param>
        /// <param name="configureServices">Optional application services configurator.</param>
        /// <param name="configureApplication">Optional application configurator.</param>
        /// <returns>A running .NET server.</returns>
        public static async Task<RunningAspNetServer> RunAspNetServerAsync( this IStObjEngineTestHelper helper,
                                                                            IEnumerable<Type> registeredTypes,
                                                                            Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator = null,
                                                                            Action<IServiceCollection>? configureServices = null,
                                                                            Action<IApplicationBuilder>? configureApplication = null )
        {
            StObjCollector collector = helper.CreateStObjCollector( registeredTypes.ToArray() );
            // These 2 services are required by the WebFrontAthService.
            collector.RegisterType( typeof( AuthenticationInfoTokenService ) );
            collector.RegisterType( typeof( StdAuthenticationTypeSystem ) );

            CompileAndLoadResult r = helper.CompileAndLoadStObjMap( collector, engineConfigurator );

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseScopedHttpContext();
            // Don't UseCKMonitoring here or the GrandOutput.Default will be reconfigured:
            // only register the IActivityMonitor and its ParallelLogger.
            builder.Services.AddScoped<IActivityMonitor, ActivityMonitor>();
            builder.Services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );

            builder.Services.AddAuthentication( WebFrontAuthOptions.OnlyAuthenticationScheme )
                        .AddWebFrontAuth();

            configureServices?.Invoke( builder.Services );

            builder.Services.AddStObjMap( helper.Monitor, r.Map );

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
