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

        public PerfectEventSender<IEvent> ImmediateSender => _hub._immediate;

        public PerfectEventSender<IEvent> AllEventSender => _hub._allEvent;
    }

}
