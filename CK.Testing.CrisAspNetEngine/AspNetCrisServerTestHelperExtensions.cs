using CK.AspNet.Auth;
using CK.Core;
using CK.Cris.AspNet;
using CK.Setup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Testing
{
    /// <summary>
    /// Extends <see cref="Testing.IStObjEngineTestHelper"/> to support end to end TypeScript tests.
    /// </summary>
    public static class AspNetCrisServerTestHelperExtensions
    {
        /// <summary>
        /// Creates, configures and starts a <see cref="RunningAspNetServer"/> that supports authentication and Cris endpoint.
        /// <para>
        /// Register a specialized <see cref="FakeUserDatabase"/> and/or <see cref="FakeWebFrontLoginService"/> in <see cref="BinPathConfiguration.Types"/>
        /// to override these fakes. You may also add them to <see cref="BinPathConfiguration.ExcludedTypes"/> if real authentication components are
        /// available in the types (but ultimately there must be a <see cref="CK.Auth.IUserInfoProvider"/> and a <see cref="IWebFrontAuthLoginService"/>
        /// type available).
        /// </para>
        /// </summary>
        /// <param name="configureServices">Optional application services configurator.</param>
        /// <param name="configureApplication">Optional application configurator.</param>
        /// <param name="webFrontAuthOptions">
        /// Optional authentication options configurator.
        /// By default <see cref="WebFrontAuthOptions.SlidingExpirationTime"/> is set to 10 minutes.
        /// </param>
        /// <returns>The running AspNet server.</returns>
        public static Task<RunningAspNetServer> CreateAspNetCrisServerAsync( this BinPathConfiguration binPath,
                                                                             Action<IServiceCollection>? configureServices,
                                                                             Action<IApplicationBuilder>? configureApplication,
                                                                             Action<WebFrontAuthOptions>? webFrontAuthOptions )
        {
            binPath.Types.Add( typeof( CrisAspNetService ) );

            static void ConfigureApplication( IApplicationBuilder app, Action<IApplicationBuilder>? configureApplication )
            {
                app.UseCris();
                configureApplication?.Invoke( app );
            }

            return binPath.CreateAspNetAuthServerAsync( configureServices,
                                                        app => ConfigureApplication( app, configureApplication ),
                                                        webFrontAuthOptions );
        }

        /// <summary>
        /// Creates, configures and starts a <see cref="RunningAspNetServer"/> that supports authentication and Cris endpoint
        /// and creates and runs a <see cref="TSTestHelperExtensions.Runner"/> that "yarn test" (with a "CRIS_ENDPOINT_URL" environment variable
        /// that is the address of the server) the <see cref="TypeScriptBinPathAspectConfiguration.TargetProjectPath"/>.
        /// <para>
        /// Register a specialized <see cref="FakeUserDatabase"/> and/or <see cref="FakeWebFrontLoginService"/> in <see cref="BinPathConfiguration.Types"/>
        /// to override these fakes. You may also add them to <see cref="BinPathConfiguration.ExcludedTypes"/> if real authentication components are
        /// available in the types (but ultimately there must be a <see cref="CK.Auth.IUserInfoProvider"/> and a <see cref="IWebFrontAuthLoginService"/>
        /// type available).
        /// </para>
        /// </summary>
        /// <param name="binPath">This BinPath.</param>
        /// <param name="configureServices">Optional application services configurator.</param>
        /// <param name="configureApplication">Optional application configurator.</param>
        /// <param name="beforeRun">
        /// Optional asynchronous action called right before tests run.
        /// Can be used to provide disposal actions and/or breakpoints or suspension or to interact with
        /// the running application services <see cref="RunningAspNetServer.Services"/>.
        /// </param>
        /// <param name="resume">
        /// Wait callback called when <see cref="Debugger.IsAttached"/> is true before running test.
        /// See <see cref="Monitoring.IMonitorTestHelperCore.SuspendAsync(Func{bool, bool}, string?, int, string?)"/>.
        /// </param>
        /// <param name="webFrontAuthOptions">
        /// Optional authentication options configurator.
        /// By default <see cref="WebFrontAuthOptions.SlidingExpirationTime"/> is set to 10 minutes.
        /// </param>
        /// <param name="testName">Automatically set by the Roslyn compiler.</param>
        /// <param name="lineNumber">Automatically set by the Roslyn compiler.</param>
        /// <param name="lineNumber">Automatically set by the Roslyn compiler.</param>
        /// <returns>The awaitable that will be completed once the TypeScript test ends.</returns>
        public static async Task RunCrisTypeScriptTestsAsync( this BinPathConfiguration binPath,
                                                              Action<IServiceCollection>? configureServices = null,
                                                              Action<IApplicationBuilder>? configureApplication = null,
                                                              Func<TSTestHelperExtensions.Runner, Task>? beforeRun = null,
                                                              Func<bool, bool>? resume = null,
                                                              Action<WebFrontAuthOptions>? webFrontAuthOptions = null,
                                                              [CallerMemberName] string? testName = null,
                                                              [CallerLineNumber] int lineNumber = 0,
                                                              [CallerFilePath] string? fileName = null )
        {
            var tsBinPathAspect = binPath.FindAspect<TypeScriptBinPathAspectConfiguration>();
            Throw.CheckArgument( "This BinPathConfiguration must be configured with a TypeScript aspect. You may call EnsureTypeScriptConfigurationAspect( targetProjectPath ) first.",
                                 tsBinPathAspect?.TargetProjectPath.IsEmptyPath is false );

            using var _ = TestHelper.Monitor.OpenInfo( $"Running '{testName}'." );

            try
            {
                RunningAspNetServer server = await CreateAspNetCrisServerAsync( binPath, configureServices, configureApplication, webFrontAuthOptions ).ConfigureAwait( false );

                // We set the CRIS_ENDPOINT_URL environment variable: the tests can use it.
                var endpointUrl = server.ServerAddress + "/.cris";
                await using var runner = TestHelper.CreateTypeScriptRunner( tsBinPathAspect.TargetProjectPath, new Dictionary<string, string> { { "CRIS_ENDPOINT_URL", endpointUrl } } );
                if( beforeRun != null )
                {
                    await beforeRun.Invoke( runner );
                }
                await TestHelper.SuspendAsync( resume ?? Util.FuncIdentity, testName, lineNumber, fileName ).ConfigureAwait( false );
                runner.Run();
            }
            catch( Exception ex )
            {
                TestHelper.Monitor.Error( $"Error while running '{testName}'.", ex );
                throw;
            }
        }

    }
}
