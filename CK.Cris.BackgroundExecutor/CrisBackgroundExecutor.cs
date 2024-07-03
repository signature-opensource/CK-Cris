using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Background execution service of <see cref="IAbstractCommand"/>.
    /// <para>
    /// This is a scoped helper service that captures the current <see cref="AmbientServiceHub"/>
    /// so that calling <see cref="CrisBackgroundExecutorService.Submit{T}(IActivityMonitor, T, AmbientServiceHub, bool, ActivityMonitor.Token?)"/>
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
        /// <param name="ambientServicesOverride">Optional ambient service configurator that can override ambient service intances.</param>
        /// <param name="issuerToken">The issuer token to use. When null a new token is obtained from the <paramref name="monitor"/>.</param>
        /// <param name="deferredExecutionInfo">Optional explicit deferred execution info. See <see cref="CrisJob.DeferredExecutionContext"/>.</param>
        /// <param name="onExecutedCommand">Optional callback eventually called with the executed command. See <see cref="CrisJob.OnExecutedCommand"/>.</param>
        /// <param name="incomingValidationCheck">
        /// Whether incoming command validation should be done again.
        /// This should not be necessary because a command that reaches an execution context should already
        /// have been submitted to the incoming command validators.
        /// <para>
        /// When not specified, this defaults to <see cref="CoreApplicationIdentity.IsDevelopmentOrUninitialized"/>:
        /// in "#Dev" or when the identity is not yet settled, the incoming validation is ran.
        /// </para>
        /// </param>
        /// <returns>The executing command that can be used to track the execution.</returns>
        public IExecutingCommand<T> Submit<T>( IActivityMonitor monitor,
                                               T command,
                                               Action<AmbientServiceHub>? ambientServicesOverride = null,
                                               ActivityMonitor.Token? issuerToken = null,
                                               IDeferredCommandExecutionContext? deferredExecutionInfo = null,
                                               Func<IActivityMonitor, IExecutedCommand, IServiceProvider?, Task>? onExecutedCommand = null,
                                               bool? incomingValidationCheck = null )
            where T : class, IAbstractCommand
        {
            var ubiq = _ambientServiceHub;
            if( ambientServicesOverride != null )
            {
                ubiq = _ambientServiceHub.CleanClone();
                ambientServicesOverride( ubiq );
            }
            return _service.Submit( monitor, command, ubiq, issuerToken, deferredExecutionInfo, onExecutedCommand, incomingValidationCheck );
        }
    }

}
