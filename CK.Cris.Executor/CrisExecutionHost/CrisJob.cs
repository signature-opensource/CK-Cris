using CK.Core;
using CK.PerfectEvent;

namespace CK.Cris
{
    /// <summary>
    /// Base class for asynchronous Cris job.
    /// <para>
    /// A job can have no <see cref="IExecutingCommand"/>. An Executing command is on the caller side,
    /// it is one of the possible interface to the execution, not the execution itself.
    /// </para>
    /// </summary>
    public sealed partial class CrisJob
    {
        readonly IAbstractCommand _command;
        readonly ActivityMonitor.Token _issuerToken;
        internal readonly AbstractCommandExecutor _executor;
        internal readonly bool _skipValidation;
        internal readonly ExecutingCommand? _executingCommand;
        readonly EndpointDefinition.ScopedData? _scopedData;

        internal IActivityMonitor? _runnerMonitor;
        internal ICrisCommandContext? _executionContext;

        /// <summary>
        /// Initializes a new <see cref="CrisJob"/>.
        /// </summary>
        /// <param name="executor">The executor that is handling this command job.</param>
        /// <param name="command">The command.</param>
        /// <param name="issuerToken">The issuer token.</param>
        /// <param name="skipValidation">Whether command validation must be skipped (because it has already been done).</param>
        /// <param name="executingCommand">The executing command if there's one.</param>
        /// <param name="scopedData">Scoped data when the execution must happen in another context.</param>
        public CrisJob( AbstractCommandExecutor executor,
                        IAbstractCommand command,
                        ActivityMonitor.Token issuerToken,
                        bool skipValidation,
                        ExecutingCommand? executingCommand,
                        EndpointDefinition.ScopedData? scopedData )
        {
            Throw.CheckNotNullArgument( executor );
            Throw.CheckNotNullArgument( command );
            Throw.CheckNotNullArgument( issuerToken );
            _executor = executor;
            _command = command;
            _issuerToken = issuerToken;
            _skipValidation = skipValidation;
            _executingCommand = executingCommand;
            _scopedData = scopedData;
        }

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
        /// Gets the scoped data that must be used to create a scoped DI container when the execution must happen in another context.
        /// This is null when the execution takes place in a front endpoint.
        /// </summary>
        public EndpointDefinition.ScopedData? ScopedData => _scopedData;

        /// <summary>
        /// Gets the runner monitor that must be available in the DI execution context.
        /// This is null until a runner starts the execution of the command.
        /// </summary>
        public IActivityMonitor? RunnerMonitor => _runnerMonitor;

        /// <summary>
        /// Gets the command execution context that must be available in the DI execution context.
        /// This is null until a runner starts the execution of the command.
        /// </summary>
        public ICrisCommandContext? ExecutionContext => _executionContext;
    }
}
