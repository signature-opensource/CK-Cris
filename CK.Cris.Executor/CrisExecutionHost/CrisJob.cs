using CK.Core;

namespace CK.Cris
{
    /// <summary>
    /// Base class for asynchronous Cris job.
    /// </summary>
    public abstract class CrisJob
    {
        internal IActivityMonitor? _runnerMonitor;
        internal ICrisExecutionContext? _executionContext;

        /// <summary>
        /// Initializes a new <see cref="CrisJob"/>.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="issuerToken">The issuer token.</param>
        protected CrisJob( IAbstractCommand command, ActivityMonitor.Token issuerToken )
        {
            Throw.CheckNotNullArgument( command );
            Command = command;
            IssuerToken = issuerToken;
        }

        /// <summary>
        /// Gets the <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>.
        /// </summary>
        public IAbstractCommand Command { get; }

        /// <summary>
        /// Gets a token that identifies the initialization of this request.
        /// </summary>
        public ActivityMonitor.Token IssuerToken { get; }
    }
}
