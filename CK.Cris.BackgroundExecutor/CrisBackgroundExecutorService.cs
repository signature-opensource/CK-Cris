using CK.Core;

namespace CK.Cris
{
    /// <summary>
    /// Background execution service of <see cref="IAbstractCommand"/>. The interface is limited to:
    /// <list type="bullet">
    ///     <item>The <see cref="Submit{T}(IActivityMonitor, T, AmbientServiceHub, bool, ActivityMonitor.Token?)"/> method to submit a command.</item>
    ///     <item>The <see cref="IExecutingCommand{T}"/> returned instance to track the execution.</item>
    /// </list>
    /// You may use the <see cref="CrisBackgroundExecutor"/> that is a scoped service and handles kindly the <see cref="AmbientServiceHub"/>.
    /// </summary>
    [Setup.AlsoRegisterType( typeof( CrisExecutionHost ) )]
    [Setup.AlsoRegisterType( typeof( CrisBackgroundDIContainerDefinition ) )]
    public class CrisBackgroundExecutorService : ContainerCommandExecutor<CrisBackgroundDIContainerDefinition.Data>, ISingletonAutoService
    {
        public CrisBackgroundExecutorService( CrisExecutionHost executionHost,
                                              IDIContainer<CrisBackgroundDIContainerDefinition.Data> endpoint )
            : base( executionHost, endpoint )
        {
        }

        /// <summary>
        /// Submits <see cref="ICommand"/> or <see cref="ICommand{TResult}"/> command.
        /// </summary>
        /// <typeparam name="T">Command type.</typeparam>
        /// <param name="monitor">The monitor.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="ambientServiceHub">Required ambient services that must be obtained from the calling context.</param>
        /// <param name="issuerToken">The issuer token to use. When null a new token is obtained from the <paramref name="monitor"/>.</param>
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
                                               AmbientServiceHub ambientServiceHub,
                                               ActivityMonitor.Token? issuerToken = null,
                                               bool? incomingValidationCheck = null )
            where T : class, IAbstractCommand
        {
            issuerToken ??= monitor.CreateToken( $"CrisBackgroundExecutor handling '{command.CrisPocoModel.PocoName}' command." );
            var cmd = new ExecutingCommand<T>( command, issuerToken );
            var scopedData = new CrisBackgroundDIContainerDefinition.Data( ambientServiceHub );
            var job = new CrisJob( this, scopedData, command, issuerToken, cmd, incomingValidationCheck );
            scopedData._job = job;
            ExecutionHost.StartJob( job );
            return cmd;
        }

    }

}
