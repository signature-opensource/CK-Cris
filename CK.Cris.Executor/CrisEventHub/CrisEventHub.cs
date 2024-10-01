using CK.Core;
using CK.PerfectEvent;

namespace CK.Cris
{
    /// <summary>
    /// Exposes the routed events to anyone.
    /// </summary>
    public sealed class CrisEventHub : ISingletonAutoService
    {
        internal PerfectEventSender<IEvent> _immediate;
        internal PerfectEventSender<IEvent> _all;

        /// <summary>
        /// Initialize a new hub.
        /// </summary>
        public CrisEventHub()
        {
            _immediate = new PerfectEventSender<IEvent>();
            _all = new PerfectEventSender<IEvent>();
            _immediate.CreateRelay( _all );
        }

        /// <summary>
        /// Raised on immediate events (see <see cref="ImmediateEventAttribute"/>).
        /// </summary>
        public PerfectEvent<IEvent> Immediate => _immediate.PerfectEvent;

        /// <summary>
        /// Raised on immediate events an routed events (see <see cref="RoutedEventAttribute"/>).
        /// </summary>
        public PerfectEvent<IEvent> All => _all.PerfectEvent;
    }

}
