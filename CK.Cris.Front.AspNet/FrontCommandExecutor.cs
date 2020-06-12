using CK.AspNet;
using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Cris
{

    [CK.Setup.ContextBoundDelegation( "CK.Setup.Cris.FrontCommandExecutorImpl, CK.Cris.Front.AspNet.Runtime" )]
    public abstract class FrontCommandExecutor : ISingletonAutoService
    {
        public FrontCommandExecutor()
        {
        }

        //public abstract Task<CommandResult> Execute( ICommand command );
    }
}
