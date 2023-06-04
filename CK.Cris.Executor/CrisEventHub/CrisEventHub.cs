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

        public CrisEventHub()
        {
            _immediate = new PerfectEventSender<IEvent>();
            _all = new PerfectEventSender<IEvent>();
            _immediate.CreateRelay( _all );
        }

        public PerfectEvent<IEvent> Immediate => _immediate.PerfectEvent;

        public PerfectEvent<IEvent> All => _all.PerfectEvent;
    }

}
