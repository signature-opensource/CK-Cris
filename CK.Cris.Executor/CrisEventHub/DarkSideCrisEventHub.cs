using CK.Core;
using CK.PerfectEvent;

namespace CK.Cris
{
    /// <summary>
    /// Source of the <see cref="CrisEventHub"/> events. This service enables events to be raised by exposing the
    /// <see cref="ImmediateSender"/> and <see cref="AllSender"/>, it also allows events to be dispatched outside
    /// of a command handler.
    /// </summary>
    [Setup.AlsoRegisterType( typeof( CrisEventHub ) )]
    public sealed class DarkSideCrisEventHub : ISingletonAutoService
    {
        readonly CrisEventHub _hub;
        readonly PocoDirectory _pocoDirectory;

        /// <summary>
        /// Initializes a new <see cref="DarkSideCrisEventHub"/>.
        /// </summary>
        /// <param name="hub">The Cris event hub.</param>
        /// <param name="pocoDirectory">The Poco directory.</param>
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
