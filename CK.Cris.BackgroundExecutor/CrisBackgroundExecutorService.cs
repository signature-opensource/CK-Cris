using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Background execution service of <see cref="IAbstractCommand"/>. The interface is limited to:
    /// <list type="bullet">
    ///     <item>One of the <c>Submit</c>{T} overloads to submit a command.</item>
    ///     <item>The <see cref="IExecutingCommand{T}"/> returned instance to track the execution.</item>
    /// </list>
    /// You may use the <see cref="CrisBackgroundExecutor"/> that is a scoped service and handles kindly the <see cref="AmbientServiceHub"/>.
    /// </summary>
    [Setup.AlsoRegisterType( typeof( CrisExecutionHost ) )]
    [Setup.AlsoRegisterType( typeof( CrisBackgroundDIContainerDefinition ) )]
    public class CrisBackgroundExecutorService : ContainerCommandExecutor<CrisBackgroundDIContainerDefinition.Data>, ISingletonAutoService
    {
        /// <summary>
        /// Initializes a new <see cref="CrisBackgroundExecutorService"/>.
        /// </summary>
        /// <param name="executionHost">The shared execution host.</param>
        /// <param name="container">The current DI container.</param>
        public CrisBackgroundExecutorService( CrisExecutionHost executionHost,
                                              IDIContainer<CrisBackgroundDIContainerDefinition.Data> container )
            : base( executionHost, container )
        {
        }

        /// <summary>
        /// Overridden to either:
        /// <list type="bullet">
        ///     <item>Creates the <see cref="AsyncServiceScope"/> if the <paramref name="job"/> has a <see cref="AmbientServiceHub"/>.</item>
        ///     <item>Restores a <see cref="AmbientServiceHub"/> from the command before creating the <see cref="AsyncServiceScope"/>.</item>
        /// </list>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="job">The starting job.</param>
        /// <returns>An error xor the configured DI scope to use.</returns>
        protected override ValueTask<(ICrisResultError?,AsyncServiceScope)> PrepareJobAsync( IActivityMonitor monitor, CrisJob job )
        {
            var scopedData = Unsafe.As<CrisBackgroundDIContainerDefinition.Data>( job.ScopedData );
            if( scopedData.AmbientServiceHub != null )
            {
                return base.PrepareJobAsync( monitor, job );
            }
            return RestoreAsync( monitor, job, scopedData );
        }

        async ValueTask<(ICrisResultError?,AsyncServiceScope)> RestoreAsync( IActivityMonitor monitor, CrisJob job, CrisBackgroundDIContainerDefinition.Data scopedData )
        {
            var (error, hub) = await ExecutionHost.RawCrisExecutor.RestoreAmbientServicesAsync( monitor, job.Command );
            if( error != null ) return (error,default);
            scopedData.AmbientServiceHub = hub;
            return await base.PrepareJobAsync( monitor, job );
        }

        /// <summary>
        /// Submits <see cref="ICommand"/> or <see cref="ICommand{TResult}"/> command.
        /// </summary>
        /// <typeparam name="T">Command type.</typeparam>
        /// <param name="command">The command to execute.</param>
        /// <param name="ambientServiceHub">
        /// Ambient services obtained from the calling context. When null, a default ambient services is automatically configured
        /// by [RestoreAmbientServices] methods for the command.
        /// </param>
        /// <param name="issuerToken">The issuer token to use.</param>
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
        public IExecutingCommand<T> Submit<T>( T command,
                                               AmbientServiceHub? ambientServiceHub,
                                               ActivityMonitor.Token issuerToken,
                                               IDeferredCommandExecutionContext? deferredExecutionInfo,
                                               Func<IActivityMonitor, IExecutedCommand, IServiceProvider?, Task>? onExecutedCommand = null,
                                               bool? incomingValidationCheck = null )
            where T : class, IAbstractCommand
        {
            Throw.CheckNotNullArgument( issuerToken );
            var cmd = new ExecutingCommand<T>( command, issuerToken );
            var scopedData = new CrisBackgroundDIContainerDefinition.Data( ambientServiceHub );
            var job = new CrisJob( this, scopedData, command, issuerToken, cmd, deferredExecutionInfo, onExecutedCommand, incomingValidationCheck );
            scopedData._job = job;
            ExecutionHost.StartJob( job );
            return cmd;
        }

        /// <summary>
        /// Submits <see cref="ICommand"/> or <see cref="ICommand{TResult}"/> command.
        /// </summary>
        /// <typeparam name="T">Command type.</typeparam>
        /// <param name="monitor">The monitor.</param>
        /// <param name="command">The command to execute.</param>
        /// <param name="ambientServiceHub">
        /// Ambient services obtained from the calling context. When null, a default ambient services is automatically configured
        /// by [RestoreAmbientServices] methods for the command.
        /// </param>
        /// <param name="issuerToken">The issuer token to use. When null a new token is obtained from the <paramref name="monitor"/>. See <see cref="CrisJob.OnExecutedCommand"/>.</param>
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
                                               AmbientServiceHub? ambientServiceHub,
                                               ActivityMonitor.Token? issuerToken = null,
                                               IDeferredCommandExecutionContext? deferredExecutionInfo = null,
                                               Func<IActivityMonitor, IExecutedCommand, IServiceProvider?, Task>? onExecutedCommand = null,
                                               bool? incomingValidationCheck = null )
            where T : class, IAbstractCommand
        {
            issuerToken ??= monitor.CreateToken( $"CrisBackgroundExecutor handling '{command.CrisPocoModel.PocoName}' command." );
            return Submit( command, ambientServiceHub, issuerToken, deferredExecutionInfo, onExecutedCommand, incomingValidationCheck );
        }

    }

}
