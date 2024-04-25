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
        readonly AmbientServiceHub _ambientServiceHub;

        public CrisBackgroundExecutor( CrisBackgroundExecutorService service, AmbientServiceHub ambientServices )
        {
            _service = service;
            _ambientServiceHub = ambientServices;
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
        /// <param name="ambientServicesOverride">Optional ambient service configurator that can override ambient service intances.</param>
        /// <param name="issuerToken">The issuer token to use. When null a new token is obtained from the <paramref name="monitor"/>.</param>
        /// <returns>The executing command that can be used to track the execution.</returns>
        public IExecutingCommand<T> Submit<T>( IActivityMonitor monitor,
                                               T command,
                                               bool skipValidation = false,
                                               Action<AmbientServiceHub>? ambientServicesOverride = null,
                                               ActivityMonitor.Token? issuerToken = null )
            where T : class, IAbstractCommand
        {
            var ubiq = _ambientServiceHub;
            if( ambientServicesOverride != null )
            {
                ubiq = _ambientServiceHub.CleanClone();
                ambientServicesOverride( ubiq );
            }
            return _service.Submit( monitor, command, ubiq, skipValidation, issuerToken );
        }
    }

}
