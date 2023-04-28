using System;

namespace CK.Cris
{
    /// <summary>
    /// This interface can appear in any command validator, handler or post handler parameters
    /// to send events during command processing.
    /// </summary>
    public interface ICrisEventSender
    {
        /// <summary>
        /// Sends an event.
        /// <para>
        /// Command executors must do their best to always be able to handle <see cref="IEvent"/> emission,
        /// and emitting events must be easy, not requiring an asynchronous context: this is why this is a
        /// simple void function.
        /// </para>
        /// </summary>
        /// <param name="e">The event to send.</param>
        void Send( IEvent e );

        /// <summary>
        /// Instantiates an event a provides a way to configure it before sending it.
        /// Once sent, the configured event is returned.
        /// <para>
        /// Command executors must do their best to always be able to handle <see cref="IEvent"/> emission,
        /// and emitting events must be easy, not requiring an asynchronous context.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <param name="configure">A function that configures the event.</param>
        T Send<T>( Action<T> configure ) where T : IEvent;
    }
}
