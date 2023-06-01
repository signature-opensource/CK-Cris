using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.Cris
{
    [EndpointDefinition]
    public abstract class CrisBackgroundEndpointDefinition : EndpointDefinition<CrisJob>
    {
        public override void ConfigureEndpointServices( IServiceCollection services,
                                                        Func<IServiceProvider,CrisJob> scopeData,
                                                        IServiceProviderIsService globalServiceExists )
        {
            services.AddScoped( sp => scopeData( sp )._runnerMonitor! );
            services.AddScoped( sp => scopeData( sp )._executionContext! );
            services.AddScoped<ICrisCallContext>( sp => scopeData( sp )._executionContext! );
        }
    }


}
