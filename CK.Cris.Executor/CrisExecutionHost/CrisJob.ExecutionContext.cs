using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Cris
{
    public sealed partial class CrisJob
    {
        [ExcludeCKType]
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

            protected override async Task RaiseImmediateEventAsync( IActivityMonitor monitor, IEvent routedImmediateEvent )
            {
                await base.RaiseImmediateEventAsync( monitor, routedImmediateEvent );
                await _job._executor.RaiseImmediateEventAsync( monitor, _job, routedImmediateEvent );
            }

            protected override Task RaiseCallerOnlyImmediateEventAsync( IActivityMonitor monitor, IEvent callerImmediateEvent )
            {
                return _job._executor.RaiseImmediateEventAsync( monitor, _job, callerImmediateEvent );
            }
        }
    }
}
