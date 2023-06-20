using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;

namespace CK.Cris
{
    [EndpointDefinition( EndpointKind.Back )]
    public abstract class CrisBackgroundEndpointDefinition : EndpointDefinition<CrisBackgroundEndpointDefinition.Data>
    {
        public sealed class Data : ScopedData
        {
            [AllowNull]
            internal CrisJob _job;

            internal Data( EndpointUbiquitousInfo ubiquitousInfo )
                : base( ubiquitousInfo ) 
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
