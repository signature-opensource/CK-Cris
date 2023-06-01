using CK.Core;

namespace CK.Cris
{
    /// <summary>
    /// Non generic base class for asynchronous Cris job.
    /// This must be specialized for specific end point by using .
    /// <para>
    /// This is used on the receiver side (by deserializing the incoming message payload). Specializations typically add
    /// specific data and should keep them internal.
    /// </para>
    /// </summary>
    public abstract class CrisAsyncJob
    {
        internal IActivityMonitor? _runnerMonitor;

        /// <summary>
        /// Initializes a new <see cref="CrisAsyncJob"/> from its data (typically used when
        /// deserializing).
        /// </summary>
        /// <param name="payload">The command.</param>
        /// <param name="issuerToken">The issuer token.</param>
        protected CrisAsyncJob( IAbstractCommand payload, ActivityMonitor.Token issuerToken )
        {
            Throw.CheckNotNullArgument( payload );
            Payload = payload;
            IssuerToken = issuerToken;
        }

        /// <summary>
        /// Gets the <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>.
        /// </summary>
        public IAbstractCommand Payload { get; }

        /// <summary>
        /// Gets a token that identifies the initialization of this request.
        /// </summary>
        public ActivityMonitor.Token IssuerToken { get; }
    }
}
