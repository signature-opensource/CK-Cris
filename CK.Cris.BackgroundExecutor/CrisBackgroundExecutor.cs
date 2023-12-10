using CK.Core;
using System;

namespace CK.Cris
{
    /// <summary>
    /// Background execution service of <see cref="IAbstractCommand"/>.
    /// <para>
    /// This is a scoped helper service that captures the current <see cref="EndpointUbiquitousInfo"/>
    /// so that calling <see cref="CrisBackgroundExecutorService.Submit{T}(IActivityMonitor, T, EndpointUbiquitousInfo, bool, ActivityMonitor.Token?)"/>
    /// is simpler.
    /// </para>
    /// </summary>
    public class CrisBackgroundExecutor : IScopedAutoService
    {
        readonly CrisBackgroundExecutorService _service;
        readonly EndpointUbiquitousInfo _ubiquitousInfo;

        public CrisBackgroundExecutor( CrisBackgroundExecutorService service, EndpointUbiquitousInfo ubiquitousInfo )
        {
            _service = service;
            _ubiquitousInfo = ubiquitousInfo;
        }

        /// <summary>
        /// Gets the actual executor service.
        /// </summary>
        public CrisBackgroundExecutorService ExecutorService => _service;

        /// <summary>
        /// Submits <see cref="ICommand"/> or <see cref="ICommand{TResult}"/> command.
        /// </summary>
        /// <typeparam name="T">Command type.</typeparam>
        /// <param name="monitor">The monitor.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="skipValidation">True to skip command validation. This should be true only if the command has already been validated.</param>
        /// <param name="ubiquitousInfoOverride">Optional ubiquitous service configurator that can override ubiquitous service intances.</param>
        /// <param name="issuerToken">The issuer token to use. When null a new token is obtained from the <paramref name="monitor"/>.</param>
        /// <returns>The executing command that can be used to track the execution.</returns>
        public IExecutingCommand<T> Submit<T>( IActivityMonitor monitor,
                                               T command,
                                               bool skipValidation = false,
                                               Action<EndpointUbiquitousInfo>? ubiquitousInfoOverride = null,
                                               ActivityMonitor.Token? issuerToken = null )
            where T : class, IAbstractCommand
        {
            var ubiq = _ubiquitousInfo;
            if( ubiquitousInfoOverride != null )
            {
                ubiq = _ubiquitousInfo.CleanClone();
                ubiquitousInfoOverride( ubiq );
            }
            return _service.Submit( monitor, command, ubiq, skipValidation, issuerToken );
        }
    }

}
