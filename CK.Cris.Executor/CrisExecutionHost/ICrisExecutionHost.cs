using CK.Core;
using CK.PerfectEvent;

namespace CK.Cris
{
    /// <summary>
    /// A Cris execution host handles <see cref="CrisJob"/> in the background
    /// thanks to a variable number of parallel runners.
    /// <para>
    /// The <see cref="ParallelRunnerCount"/> defaults to 1 but this can be changed anytime.
    /// </para>
    /// <para>
    /// This is a multiple interface: there may be more than one execution host in a system
    /// but one is enough.
    /// </para>
    /// </summary>
    [IsMultiple]
    public interface ICrisExecutionHost
    {
        /// <summary>
        /// Gets or sets the number of parallel runners that handle the submitted commands.
        /// It must be between 1 and 1000.
        /// </summary>
        int ParallelRunnerCount { get; set; }

        /// <summary>
        /// Raised whenever the <see cref="ParallelRunnerCount"/> changes.
        /// </summary>
        PerfectEvent<ICrisExecutionHost> ParallelRunnerCountChanged { get; }
    }
}
