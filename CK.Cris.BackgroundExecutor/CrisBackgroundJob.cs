using CK.Auth;
using CK.Core;

namespace CK.Cris
{
    public sealed class CrisBackgroundJob : CrisJob
    {
        internal readonly ExecutingCommand _command;
        internal readonly IAuthenticationInfo _authenticationInfo;

        internal CrisBackgroundJob( CrisBackgroundExecutor executor,
                                    ExecutingCommand command,
                                    bool skipValidation,
                                    IAuthenticationInfo authenticationInfo )
            : base( executor, command.Command, command.IssuerToken, skipValidation, command )
        {
            _command = command;
            _authenticationInfo = authenticationInfo;
        }

    }
}
