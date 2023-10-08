using CK.Auth;
using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace CK.Cris
{
    /// <summary>
    /// Background execution of <see cref="IAbstractCommand"/>. The interface is limited to:
    /// <list type="bullet">
    ///     <item>The <see cref="Start{T}(IActivityMonitor, T, EndpointUbiquitousInfo, bool, ActivityMonitor.Token?)"/> method to submit a command.</item>
    ///     <item>The <see cref="IExecutingCommand{T}"/> returned instance to track the execution.</item>
    /// </list>
    /// </summary>
    [Setup.AlsoRegisterType( typeof( CrisExecutionHost ) )]
    [Setup.AlsoRegisterType( typeof( CrisBackgroundEndpointDefinition ) )]
    public class CrisBackgroundExecutor : EndpointCommandExecutor<CrisBackgroundEndpointDefinition.Data>, ISingletonAutoService
    {
        public CrisBackgroundExecutor( CrisExecutionHost executionHost,
                                       IEndpointType<CrisBackgroundEndpointDefinition.Data> endpoint )
            : base( executionHost, endpoint )
        {
        }

        /// <summary>
        /// Submits <see cref="ICommand"/> or <see cref="ICommand{TResult}"/> command.
        /// </summary>
        /// <typeparam name="T">Command type.</typeparam>
        /// <param name="monitor">The monitor.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="ubiquitousInfo">Required ubiquitous informations that must be obtained from the calling context.</param>
        /// <param name="skipValidation">True to skip command validation. This should be true only if the command has already been validated.</param>
        /// <param name="issuerToken">The issuer token to use. When null a new token is obtained from the <paramref name="monitor"/>.</param>
        /// <returns>The executing command that can be used to track the execution.</returns>
        public IExecutingCommand<T> Start<T>( IActivityMonitor monitor,
                                              T command,
                                              EndpointUbiquitousInfo ubiquitousInfo,
                                              bool skipValidation = false,
                                              ActivityMonitor.Token? issuerToken = null )
            where T : class, IAbstractCommand
        {
            issuerToken ??= monitor.CreateToken( $"CrisBackgroundExecutor handling '{command.CrisPocoModel.PocoName}' command." );
            var cmd = new ExecutingCommand<T>( command, issuerToken );
            var scopedData = new CrisBackgroundEndpointDefinition.Data( ubiquitousInfo );
            var job = new CrisJob( this, scopedData, command, issuerToken, skipValidation, cmd );
            scopedData._job = job;
            ExecutionHost.StartJob( job );
            return cmd;
        }

    }
}
