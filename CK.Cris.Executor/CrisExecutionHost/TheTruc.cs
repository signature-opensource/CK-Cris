using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace CK.Cris
{
    public abstract class TheTruc
    {
        internal protected abstract CrisJob CreateJob( IAbstractCommand command, ActivityMonitor.Token issuerToken );

        internal protected abstract AsyncServiceScope CreateAsyncScope( CrisJob job );

        internal protected abstract Task ReturnCommandResultAsync( IActivityMonitor monitor, CrisJob job, CrisExecutionHost.ICrisJobResult r );

        internal protected abstract Task ReturnCrisValidationResultAsync( IActivityMonitor monitor, CrisJob job, CrisValidationResult validation );

        internal protected abstract Task ReturnEventAsync( IActivityMonitor monitor, IEvent e );
    }

}
