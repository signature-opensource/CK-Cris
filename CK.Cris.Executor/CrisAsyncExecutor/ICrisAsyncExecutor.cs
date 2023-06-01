using CK.Core;
using CK.PerfectEvent;

namespace CK.Cris
{
    /// <summary>
    /// A Cris executor handles <see cref="CrisAsyncJob"/> in the background in the context
    /// of a <see cref="EndpointType"/> thanks to a variable count of parallel runners.
    /// </summary>
    [IsMultiple]
    public interface ICrisAsyncExecutor : ISingletonAutoService
    {
        /// <summary>
        /// Gets the endpoint that hosts the execution of commands.
        /// </summary>
        IEndpointType EndpointType { get; }

        /// <summary>
        /// Gets or sets the number of parallel runners that handle the requests.
        /// It must be between 1 and 1000.
        /// </summary>
        int ParallelRunnerCount { get; set; }

        /// <summary>
        /// Raised whenever the <see cref="ParallelRunnerCount"/> changes.
        /// </summary>
        PerfectEvent<ICrisAsyncExecutor> ParallelRunnerCountChanged { get; }
    }
}
