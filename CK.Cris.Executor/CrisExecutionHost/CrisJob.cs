using CK.Core;
using CK.PerfectEvent;

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
    public sealed partial class CrisJob
    {
        readonly IAbstractCommand _command;
        internal readonly DIContainerDefinition.IScopedData _scopedData;
        readonly ActivityMonitor.Token _issuerToken;
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
        /// <param name="incomingValidationCheck">
        /// Whether incoming command validation should be done again.
        /// This should not be necessary because a command that reaches an execution context should already
        /// have been submitted to the incoming command validators.
        /// <para>
        /// When not specified, this defaults to <see cref="CoreApplicationIdentity.IsDevelopmentOrUninitialized"/>:
        /// in "#Dev" or when the identity is not yet settled, the incoming validation is ran.
        /// </para>
        /// </param>
        public CrisJob( ContainerCommandExecutor executor,
                        DIContainerDefinition.IScopedData scopedData,
                        IAbstractCommand command,
                        ActivityMonitor.Token issuerToken,
                        ExecutingCommand? executingCommand,
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
            _scopedData = scopedData;
        }

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
        /// Gets whether an executing command has been provided to this job.
        /// </summary>
        public bool HasExecutingCommand => _executingCommand != null;

        /// <summary>
        /// Gets whether the command must be validated as if it entered the system.
        /// </summary>
        public bool IncomingValidationCheck => _incomingValidationCheck;

        /// <summary>
        /// Gets the runner monitor that will be available in the DI execution context.
        /// This is null until a runner starts the execution of the command.
        /// </summary>
        public IActivityMonitor? RunnerMonitor => _runnerMonitor;

        /// <summary>
        /// Gets the command execution context that will be available in the DI execution context.
        /// This is null until a runner starts the execution of the command.
        /// </summary>
        public ICrisCommandContext? ExecutionContext => _executionContext;
    }
}
