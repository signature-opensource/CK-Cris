using CK.Core;
using CK.PerfectEvent;

namespace CK.Cris
{
    /// <summary>
    /// Base class for asynchronous Cris job.
    /// <para>
    /// A job can have no <see cref="IExecutedCommand"/>. Executing commands are on the caller side
    /// they are one possible interface to the execution, not the execution itself.
    /// </para>
    /// </summary>
    public abstract partial class CrisJob
    {
        internal readonly AbstractCommandExecutor _executor;
        internal readonly bool _skipValidation;
        internal readonly ExecutingCommand? _executingCommand;
        internal IActivityMonitor? _runnerMonitor;
        internal ICrisExecutionContext? _executionContext;

        /// <summary>
        /// Initializes a new <see cref="CrisJob"/>.
        /// </summary>
        /// <param name="executor">The executor that is handling this command job.</param>
        /// <param name="command">The command.</param>
        /// <param name="issuerToken">The issuer token.</param>
        /// <param name="skipValidation">Whether command validation must be skipped (because it has already been done).</param>
        /// <param name="executingCommand">The executing command if there's one.</param>
        protected CrisJob( AbstractCommandExecutor executor,
                           IAbstractCommand command,
                           ActivityMonitor.Token issuerToken,
                           bool skipValidation,
                           ExecutingCommand? executingCommand )
        {
            Throw.CheckNotNullArgument( executor );
            Throw.CheckNotNullArgument( command );
            Throw.CheckNotNullArgument( issuerToken );
            _executor = executor;
            Command = command;
            IssuerToken = issuerToken;
            _skipValidation = skipValidation;
            _executingCommand = executingCommand;
        }

        /// <summary>
        /// Gets the <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>.
        /// </summary>
        public IAbstractCommand Command { get; }

        /// <summary>
        /// Gets the correlation identifier: a token that identifies the initialization of this job.
        /// </summary>
        public ActivityMonitor.Token IssuerToken { get; }

        /// <summary>
        /// Gets whether an executing command has been provided to this job.
        /// </summary>
        public bool HasExecutingCommand => _executingCommand != null;
    }
}
