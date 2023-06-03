using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Cris
{
    public abstract partial class CrisJob
    {
        internal sealed class ExecutionContext : CrisExecutionContext
        {
            readonly CrisJob _job;

            public ExecutionContext( CrisJob job,
                                     IActivityMonitor monitor,
                                     IServiceProvider serviceProvider,
                                     PocoDirectory pocoDirectory,
                                     RawCrisExecutor rawExecutor )
                : base( monitor, serviceProvider, pocoDirectory, rawExecutor )
            {
                _job = job;
            }

            protected override Task RaiseImmediateEventAsync( IActivityMonitor monitor, IEvent e )
            {
                return _job._executor.RaiseImmediateEventAsync( monitor, _job, e );
            }
        }
    }
}
