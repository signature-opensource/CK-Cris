using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;

namespace CK.Cris
{
    [DIContainerDefinition( DIContainerKind.Backend )]
    public abstract class CrisBackgroundDIContainerDefinition : DIContainerDefinition<CrisBackgroundDIContainerDefinition.Data>
    {
        public sealed class Data : BackendScopedData
        {
            [AllowNull]
            internal CrisJob _job;

            internal Data( AmbientServiceHub ambientServiceHub )
                : base( ambientServiceHub ) 
            {
            }
        }


        public override void ConfigureEndpointServices( IServiceCollection services,
                                                        Func<IServiceProvider, Data> scopeData,
                                                        IServiceProviderIsService globalServiceExists )
        {
            services.AddScoped( sp => scopeData( sp )._job.RunnerMonitor! );
            services.AddScoped( sp => scopeData( sp )._job.RunnerMonitor!.ParallelLogger );
            services.AddScoped( sp => scopeData( sp )._job.ExecutionContext! );
            services.AddScoped<ICrisEventContext>( sp => scopeData( sp )._job.ExecutionContext! );
        }
    }


}
