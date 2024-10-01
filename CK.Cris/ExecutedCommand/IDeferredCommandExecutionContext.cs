using CK.Core;
using System;

namespace CK.Cris
{
    /// <summary>
    /// When a command is not executed inline but deferred to another execution context, this is available on
    /// the <see cref="IExecutedCommand.DeferredExecutionContext"/>.
    /// <para>
    /// </para>
    /// This abstraction can easily be implemented by any "executing command context" so that IExecutedCommand.DeferredExecutionContext
    /// can link back to it. This is an easy way to avoid dictionaries of other map data structures (at the price of a downcast).
    /// </summary>
    public interface IDeferredCommandExecutionContext
    {
        /// <summary>
        /// Gets a token that identifies the initialization of the command execution.
        /// </summary>
        ActivityMonitor.Token IssuerToken { get; }
    }
}
