using CK.Core;

namespace CK.Cris
{
    /// <summary>
    /// Base class for asynchronous Cris job.
    /// </summary>
    /// <typeparam name="TCallerInfo">The caller specific data.</typeparam>
    public abstract class CrisAsyncJob<TCallerInfo> : CrisAsyncJob where TCallerInfo : notnull
    {
        internal readonly TCallerInfo _callerInfo;

        /// <summary>
        /// Initializes a new <see cref="CrisAsyncJob"/> with is endpoint data.
        /// </summary>
        /// <param name="payload">The command.</param>
        /// <param name="issuerToken">The issuer token.</param>
        protected CrisAsyncJob( IAbstractCommand payload,
                                ActivityMonitor.Token issuerToken,
                                TCallerInfo callerInfo )
            : base( payload, issuerToken )
        {
            _callerInfo = callerInfo;
        }
    }
}
