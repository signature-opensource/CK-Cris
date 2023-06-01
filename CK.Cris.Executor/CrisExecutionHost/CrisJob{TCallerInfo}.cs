using CK.Core;

namespace CK.Cris
{
    /// <summary>
    /// Base class for asynchronous Cris job.
    /// </summary>
    /// <typeparam name="TCallerInfo">The caller specific data.</typeparam>
    public abstract class CrisJob<TCallerInfo> : CrisJob where TCallerInfo : notnull
    {
        internal readonly TCallerInfo _callerInfo;

        /// <summary>
        /// Initializes a new <see cref="CrisJob"/> with is endpoint data.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="issuerToken">The issuer token.</param>
        protected CrisJob( IAbstractCommand command,
                                ActivityMonitor.Token issuerToken,
                                TCallerInfo callerInfo )
            : base( command, issuerToken )
        {
            _callerInfo = callerInfo;
        }
    }
}
