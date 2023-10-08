using CK.Core;
using CK.PerfectEvent;

namespace CK.Cris
{
    [Setup.AlsoRegisterType( typeof( CrisEventHub ) )]
    public sealed class DarkSideCrisEventHub : ISingletonAutoService
    {
        readonly CrisEventHub _hub;
        readonly PocoDirectory _pocoDirectory;

        public DarkSideCrisEventHub( CrisEventHub hub, PocoDirectory pocoDirectory )
        {
            _hub = hub;
            _pocoDirectory = pocoDirectory;
        }

        /// <summary>
        /// Gets the poco directory.
        /// </summary>
        public PocoDirectory PocoDirectory => _pocoDirectory;

        /// <summary>
        /// Gets the immediate event sender.
        /// This sender automatically relays its events to the <see cref="AllSender"/> (see
        /// <see cref="PerfectEventSender{TEvent}.CreateRelay(PerfectEventSender{TEvent}, bool)"/>).
        /// </summary>
        public PerfectEventSender<IEvent> ImmediateSender => _hub._immediate;

        /// <summary>
        /// Gets the sender of all events.
        /// </summary>
        public PerfectEventSender<IEvent> AllSender => _hub._all;

    }

}
