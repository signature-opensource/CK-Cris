using CK.Core;
using CK.PerfectEvent;

namespace CK.Cris
{
    public sealed class CrisEventHub : ISingletonAutoService
    {
        internal PerfectEventSender<IEvent> _immediate;
        internal PerfectEventSender<IEvent> _allEvent;

        public CrisEventHub()
        {
            _immediate = new PerfectEventSender<IEvent>();
            _allEvent = new PerfectEventSender<IEvent>();
            _immediate.CreateRelay( _allEvent );
        }

        public PerfectEvent<IEvent> Immediate => _immediate.PerfectEvent;
        public PerfectEvent<IEvent> AllEvent => _allEvent.PerfectEvent;
    }

}
