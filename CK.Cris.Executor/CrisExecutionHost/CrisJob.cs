using CK.Core;
using CK.PerfectEvent;
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// A Cris job contains everything the <see cref="CrisExecutionHost"/> needs to execute
    /// a command by one of its runner.
    /// <para>
    /// A job can have no <see cref="IExecutingCommand"/>. An Executing command is on the caller side,
    /// it is one of the possible interface to the execution, not the execution itself.
    /// </para>
    /// </summary>
    public sealed partial class CrisJob : IDeferredCommandExecutionContext
    {
        readonly IAbstractCommand _command;
        internal readonly DIContainerDefinition.IScopedData _scopedData;
        readonly ActivityMonitor.Token _issuerToken;
        readonly IDeferredCommandExecutionContext _deferredExecutionContext;
        readonly Func<IActivityMonitor,IExecutedCommand,IServiceProvider?,Task>? _onExecutedCommand;
        internal readonly ContainerCommandExecutor _executor;
        internal readonly bool _incomingValidationCheck;
        internal readonly ExecutingCommand? _executingCommand;

        internal IActivityMonitor? _runnerMonitor;
        internal ICrisCommandContext? _executionContext;

        /// <summary>
        /// Initializes a new <see cref="CrisJob"/>.
        /// </summary>
        /// <param name="executor">The executor that is handling this command job.</param>
        /// <param name="scopedData">Scoped data required to create the scoped context when executing the command.</param>
        /// <param name="command">The command.</param>
        /// <param name="issuerToken">The issuer token.</param>
        /// <param name="executingCommand">The executing command if there's one.</param>
        /// <param name="deferredExecutionInfo">
        /// Optional explicit deferred execution info.
        /// When null, the <see cref="DeferredExecutionContext"/> will be the <paramref name="executingCommand"/> or this CrisJob.
        /// </param>
        /// <param name="onExecutedCommand">Optional callback eventually called with the executed command. See <see cref="CrisJob.OnExecutedCommand"/>.</param>
        /// <param name="incomingValidationCheck">
        /// Whether incoming command validation should be done again.
        /// </param>
        public CrisJob( ContainerCommandExecutor executor,
                        DIContainerDefinition.IScopedData scopedData,
                        IAbstractCommand command,
                        ActivityMonitor.Token issuerToken,
                        ExecutingCommand? executingCommand,
                        IDeferredCommandExecutionContext? deferredExecutionInfo,
                        Func<IActivityMonitor, IExecutedCommand, IServiceProvider?, Task>? onExecutedCommand,
                        bool? incomingValidationCheck = null )
        {
            Throw.CheckNotNullArgument( executor );
            Throw.CheckNotNullArgument( scopedData );
            Throw.CheckNotNullArgument( command );
            Throw.CheckNotNullArgument( issuerToken );
            _executor = executor;
            _command = command;
            _issuerToken = issuerToken;
            _incomingValidationCheck = incomingValidationCheck ?? CoreApplicationIdentity.IsDevelopmentOrUninitialized;
            _executingCommand = executingCommand;
            _deferredExecutionContext = deferredExecutionInfo ?? executingCommand ?? (IDeferredCommandExecutionContext)this;
            _onExecutedCommand = onExecutedCommand;
            _scopedData = scopedData;
        }

        ActivityMonitor.Token IDeferredCommandExecutionContext.IssuerToken => _issuerToken;

        /// <summary>
        /// Gets the scoped data that will be used by <see cref="ContainerCommandExecutor.PrepareJobAsync(IActivityMonitor, CrisJob)"/>.
        /// </summary>
        public DIContainerDefinition.IScopedData ScopedData => _scopedData;

        /// <summary>
        /// Gets the <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>.
        /// </summary>
        public IAbstractCommand Command => _command;

        /// <summary>
        /// Gets the correlation identifier: a token that identifies the initialization of this job.
        /// </summary>
        public ActivityMonitor.Token IssuerToken => _issuerToken;

        /// <summary>
        /// Gets the executing command if it has been provided to this job.
        /// </summary>
        public ExecutingCommand? ExecutingCommand => _executingCommand;

        /// <summary>
        /// Gets the <see cref="IDeferredCommandExecutionContext"/> for the command: the <see cref="IExecutedCommand.DeferredExecutionContext"/>
        /// will be set to this instance.
        /// <para>
        /// This has been provided to the construcotr, or is the <see cref="ExecutingCommand"/> if it is not null
        /// otherwise it is this <see cref="CrisJob"/>.
        /// </para>
        /// </summary>
        public IDeferredCommandExecutionContext DeferredExecutionContext => _deferredExecutionContext;

        /// <summary>
        /// Gets whether the command must be validated as if it entered the system.
        /// <para>
        /// This should not be necessary because a command that reaches an execution context should already
        /// have been submitted to the incoming command validators.
        /// </para>
        /// <para>
        /// When not specified, this defaults to <see cref="CoreApplicationIdentity.IsDevelopmentOrUninitialized"/>:
        /// in "#Dev" or when the identity is not yet settled, the incoming validation is ran.
        /// </para>
        /// <para>
        /// The trick here is that the default, in practice, is <c>true</c>: a command should always be executed
        /// in a context that is compliant with the DI container configuration and this is what the [RestoreAmbientService]
        /// methods must do. This coherency check is done in "#Dev" environment but not in production.
        /// </para>
        /// </summary>
        public bool IncomingValidationCheck => _incomingValidationCheck;

        /// <summary>
        /// Gets the <see cref="CrisExecutionHost"/> runner monitor that will be available in the
        /// DI execution context.
        /// This is null until a runner starts the execution of the command.
        /// </summary>
        public IActivityMonitor? RunnerMonitor => _runnerMonitor;

        /// <summary>
        /// Gets the command execution context that will be available in the DI execution context.
        /// This is null until a runner starts the execution of the command.
        /// </summary>
        public ICrisCommandContext? ExecutionContext => _executionContext;

        /// <summary>
        /// Gets the optional delegate that will be called once the command has been executed.
        /// This is a low level hook that should be used rarely and carefully to support advanced scenarii
        /// like emitting an event after the execution of a command.
        /// <para>
        /// This delegate is called from the command execution context: the activity monitor is the <see cref="RunnerMonitor"/>
        /// and the service provider is the configured scoped services.
        /// The service provider is null if and only if the scoped services could not be created because ambient services
        /// failed to be restored (a [RestoreAmblientService] methods threw an exception).
        /// </para>
        /// <para>
        /// Note that when this is called, the <see cref="IExecutedCommand.DeferredExecutionContext"/> is either the <see cref="ExecutingCommand"/>
        /// (if it is not null) or this <see cref="CrisJob"/> instance.
        /// </para>
        /// </summary>
        public Func<IActivityMonitor, IExecutedCommand, IServiceProvider?, Task>? OnExecutedCommand => _onExecutedCommand;

        /// <summary>
        /// Helper that creates the executed command.
        /// </summary>
        /// <param name="result">The command result. See <see cref="IExecutedCommand.Result"/>.</param>
        /// <param name="validationMessages">The command validation messages.</param>
        /// <param name="events">The non immediate events emitted by the command.</param>
        /// <returns>The final executed command.</returns>
        public ExecutedCommand CreateExecutedCommand( object? result, ImmutableArray<UserMessage> validationMessages, ImmutableArray<IEvent> events )
        {
            return _command.CrisPocoModel.CreateExecutedCommand( _command, result, validationMessages, events, _deferredExecutionContext );
        }

        internal Task SetFinalResultAsync( IActivityMonitor monitor, ExecutedCommand result, IServiceProvider? scoped )
        {
            if( _onExecutedCommand != null ) return SlowSetFinalResultAsync( monitor, result, scoped );
            _executingCommand?.DarkSide.SetResult( result );
            return _executor.SetFinalResultAsync( monitor, this, result );
        }

        async Task SlowSetFinalResultAsync( IActivityMonitor monitor, ExecutedCommand result, IServiceProvider? scoped )
        {
            Throw.DebugAssert( _onExecutedCommand != null );
            _executingCommand?.DarkSide.SetResult( result );
            await _executor.SetFinalResultAsync( monitor, this, result );
            await _onExecutedCommand( monitor, result, scoped );
        }
    }
}
