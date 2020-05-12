using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Cris.Tests
{
    public class SampleCommandHandler : IAutoService
    {
        readonly IAuthorizationServer _auth;
        readonly IMailSenderService _mailer;

        public SampleCommandHandler( IAuthorizationServer auth, IMailSenderService mailer )
        {
            _auth = auth;
            _mailer = mailer;
        }

        public void Handle( IVoidAuthorizedCommand cmd, IActivityMonitor monitor )
        {
            _auth.Check( cmd.ActorId );
            monitor.Info( $"Handling VoidAuthorized: {cmd.Parameter}, {cmd.ActorId}" );
        }
    }

    public interface IMailSenderService
    {
    }

    public interface IAuthorizationServer
    {
        void Check( int actorId );
    }
}
