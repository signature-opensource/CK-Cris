using System;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// This interface can appear in any command handler or post handler or in routed
    /// event handler method parameters.
    /// <para>
    /// It is not available in command validator methods: a validator cannot send events
    /// or execute commands and is given a validation monitor to emit its errors and
    /// warnings.
    /// </para>
    /// </summary>
    public interface ICrisExecutionContext : ICrisCallContext
    {
        /// <summary>
        /// Emits an event.
        /// This must be called sequentially, the returned task must be awaited: no
        /// parallel is supported.
        /// <para>
        /// Once emitted, the event should not be mutated otherwise kitten will die: it is safer to use
        /// <see cref="EmitEventAsync{T}(Action{T}){T}(Action{T})"/>.
        /// </para>
        /// </summary>
        /// <param name="e">The event to send.</param>
        Task EmitEventAsync( IEvent e );

        /// <summary>
        /// Emits a configured event.
        /// This must be called sequentially, the returned task must be awaited: no
        /// parallel is supported.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <param name="configure">A function that configures the event.</param>
        Task EmitEventAsync<T>( Action<T> configure ) where T : IEvent;
    }
}
