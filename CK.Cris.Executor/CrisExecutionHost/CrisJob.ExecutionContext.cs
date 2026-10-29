using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Cris;

public sealed partial class CrisJob
{
    [ExcludedCKType]
    internal sealed class JobExecutionContext : CrisExecutionContext
    {
        readonly CrisJob _job;

        public JobExecutionContext( CrisJob job,
                                    IActivityMonitor monitor,
                                    IServiceProvider serviceProvider,
                                    DarkSideCrisEventHub eventHub,
                                    RawCrisExecutor rawExecutor )
            : base( monitor, serviceProvider, eventHub, rawExecutor )
        {
            _job = job;
        }

        protected override async Task RaiseImmediateEventAsync( IActivityMonitor monitor, IEvent localImmediateEvent )
        {
            await base.RaiseImmediateEventAsync( monitor, localImmediateEvent );
            await _job._executor.RaiseImmediateEventAsync( monitor, _job, localImmediateEvent );
        }

        protected override Task RaiseCallerOnlyImmediateEventAsync( IActivityMonitor monitor, IEvent callerImmediateEvent )
        {
            return _job._executor.RaiseImmediateEventAsync( monitor, _job, callerImmediateEvent );
        }
    }
}
