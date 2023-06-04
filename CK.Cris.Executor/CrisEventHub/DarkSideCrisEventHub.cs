using CK.Core;
using CK.PerfectEvent;

namespace CK.Cris
{
    [Setup.AlsoRegisterType( typeof( CrisEventHub ) )]
    public sealed class DarkSideCrisEventHub : ISingletonAutoService
    {
        readonly CrisEventHub _hub;

        public DarkSideCrisEventHub( CrisEventHub hub )
        {
            _hub = hub;
        }
        WIP

        /// <summary>
        /// Gets the immediate event sender.
        /// This sender automatically relays its events to the <see cref="AllEventSender"/> (see
        /// <see cref="PerfectEventSender{TEvent}.CreateRelay(PerfectEventSender{TEvent}, bool)"/>).
        /// </summary>
        public PerfectEventSender<IEvent> ImmediateSender => _hub._immediate;

        /// <summary>
        /// Gets the sender of all events.
        /// </summary>
        public PerfectEventSender<IEvent> AllEventSender => _hub._allEvent;
    }

}
