using Microsoft.AspNetCore.Builder;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using CK.AspNet.Auth;

namespace CK
{
    /// <summary>
    /// Running AspNet server with its <see cref="CrisEndpointUrl"/>, <see cref="Services"/>
    /// and <see cref="Configuration"/>.
    /// </summary>
    public sealed class RunningAspNetServer : IAsyncDisposable
    {
        readonly string _serverAddress;
        readonly string _crisEndpointUrl;
        readonly WebApplication _app;

        internal RunningAspNetServer( WebApplication app, string serverAddress )
        {
            _app = app;
            _serverAddress = serverAddress;
            _crisEndpointUrl = serverAddress + "/.cris/net";
        }

        /// <summary>
        /// Gets the application's configured services.
        /// </summary>
        public IServiceProvider Services => _app.Services;

        /// <summary>
        /// Gets the application's configuration.
        /// </summary>
        public IConfiguration Configuration => _app.Configuration;

        /// <summary>
        /// Gets the server address (the port is automatically assigned).
        /// </summary>
        public string ServerAddress => _serverAddress;

        /// <summary>
        /// Gets the absolute url to send Cris command: "<see cref="ServerAddress"/>/.cris/net".
        /// </summary>
        public string CrisEndpointUrl => _crisEndpointUrl;

        /// <summary>
        /// Stops the application.
        /// </summary>
        /// <returns>The awaitable.</returns>
        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
        }
    }

}
