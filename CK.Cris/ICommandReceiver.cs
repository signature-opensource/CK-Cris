using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Command receiver called by the End Point.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    public interface ICommandReceiver<TCommand> where TCommand : ICommand
    {
        /// <summary>
        /// Gets whether the <see cref="Handle(IServiceProvider, in ReceivedCommand{TCommand})"/> implementation is
        /// a "synchronous on asynchronous" implementation.
        /// </summary>
        bool IsFakeSync { get; }

        /// <summary>
        /// Gets whether the <see cref="HandleAsync(IServiceProvider, in ReceivedCommand{TCommand})"/> implementation
        /// fakes asynchonicity: it is actually a blocking, synchronous call.
        /// </summary>
        bool IsFakeAsync { get; }

        /// <summary>
        /// Handles the command asynchronously.
        /// </summary>
        /// <param name="sp">The current service provider to use to resolve any scoped services.</param>
        /// <param name="command">The command to handle.</param>
        /// <returns>The <see cref="VISAMResponse"/>.</returns>
        Task<VISAMResponse> HandleAsync( IServiceProvider sp, ReceivedCommand<TCommand> command );

        /// <summary>
        /// Handles the command synchronously.
        /// </summary>
        /// <param name="sp">The current service provider to use to resolve any scoped services.</param>
        /// <param name="command">The command to handle.</param>
        /// <returns>The <see cref="VISAMResponse"/>.</returns>
        VISAMResponse Handle( IServiceProvider sp, ReceivedCommand<TCommand> command );
    }

}
