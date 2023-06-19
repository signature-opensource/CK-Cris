using CK.Auth;
using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace CK.Cris
{
    /// <summary>
    /// Background execution of <see cref="IAbstractCommand"/>. The interface is limited to:
    /// <list type="bullet">
    ///     <item>The <see cref="Start{T}(IActivityMonitor, T, IAuthenticationInfo?, bool, ActivityMonitor.Token?)"/> method to submit a command.</item>
    ///     <item>The <see cref="IExecutingCommand{T}"/> returned instance to track the execution.</item>
    /// </list>
    /// </summary>
    [Setup.AlsoRegisterType( typeof( CrisExecutionHost ) )]
    [Setup.AlsoRegisterType( typeof( CrisBackgroundEndpointDefinition ) )]
    public class CrisBackgroundExecutor : AbstractCommandExecutor, ISingletonAutoService
    {
        readonly CrisExecutionHost _executionHost;
        readonly IEndpointType<CrisBackgroundJob> _endpoint;
        readonly IAuthenticationTypeSystem _authenticationTypeSystem;

        public CrisBackgroundExecutor( CrisExecutionHost executionHost,
                                       IEndpointType<CrisBackgroundJob> endpoint,
                                       IAuthenticationTypeSystem authenticationTypeSystem )
        {
            _executionHost = executionHost;
            _endpoint = endpoint;
            _authenticationTypeSystem = authenticationTypeSystem;
        }

        /// <summary>
        /// Gets the execution host that is used by this executor.
        /// The same execution host can be used by multiple executors at the same time since 
        /// host's responsibility is only to manage the runners.
        /// </summary>
        public ICrisExecutionHost ExecutionHost => _executionHost;

        /// <summary>
        /// Submits <see cref="ICommand"/> or <see cref="ICommand{TResult}"/> command.
        /// </summary>
        /// <typeparam name="T">Command type.</typeparam>
        /// <param name="monitor">The monitor.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="authenticationInfo">Optional authentication info.</param>
        /// <param name="skipValidation">True to skip command validation. This should be true only if the command has already been validated.</param>
        /// <param name="issuerToken">The issuer token to use. When null a new token is obtained from the <paramref name="monitor"/>.</param>
        /// <returns>The executing command that can be used to track the execution.</returns>
        public IExecutingCommand<T> Start<T>( IActivityMonitor monitor,
                                              T command,
                                              IAuthenticationInfo? authenticationInfo = null,
                                              bool skipValidation = false,
                                              ActivityMonitor.Token? issuerToken = null )
            where T : class, IAbstractCommand
        {
            issuerToken ??= monitor.CreateToken( $"CrisBackgroundExecutor handling '{command.CrisPocoModel.PocoName}' command." );
            var cmd = new ExecutingCommand<T>( command, issuerToken );
            var job = new CrisBackgroundJob( this, cmd, skipValidation, authenticationInfo ?? _authenticationTypeSystem.AuthenticationInfo.None );
            _executionHost.StartJob( job );
            return cmd;
        }

        static CrisBackgroundJob B( CrisJob job ) => Unsafe.As<CrisBackgroundJob>( job );

        protected override AsyncServiceScope CreateAsyncScope( CrisJob job )
        {
            return _endpoint.GetContainer().CreateAsyncScope( B( job ) );
        }

    }
}
