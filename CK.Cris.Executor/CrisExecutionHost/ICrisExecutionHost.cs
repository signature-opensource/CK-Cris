using CK.Core;
using CK.PerfectEvent;

namespace CK.Cris
{
    /// <summary>
    /// A Cris execution host handles <see cref="CrisJob"/> in the background in
    /// thanks to a variable count of parallel runners.
    /// </summary>
    [IsMultiple]
    public interface ICrisExecutionHost
    {
        /// <summary>
        /// Gets or sets the number of parallel runners that handle the requests.
        /// It must be between 1 and 1000.
        /// </summary>
        int ParallelRunnerCount { get; set; }

        /// <summary>
        /// Raised whenever the <see cref="ParallelRunnerCount"/> changes.
        /// </summary>
        PerfectEvent<ICrisExecutionHost> ParallelRunnerCountChanged { get; }
    }
}
