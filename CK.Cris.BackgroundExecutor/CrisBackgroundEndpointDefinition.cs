using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.Cris
{
    [EndpointDefinition]
    public abstract class CrisBackgroundEndpointDefinition : EndpointDefinition<CrisBackgroundJob>
    {
        public override void ConfigureEndpointServices( IServiceCollection services,
                                                        Func<IServiceProvider, CrisBackgroundJob> scopeData,
                                                        IServiceProviderIsService globalServiceExists )
        {
            CrisExecutionHost.StandardConfigureEndpoint( services, scopeData );
            services.AddScoped( sp => scopeData( sp )._authenticationInfo );
        }
    }


}
