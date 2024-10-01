using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;

namespace CK.Cris
{
    /// <summary>
    /// Background container definition that host background command execution.
    /// </summary>
    [DIContainerDefinition( DIContainerKind.Background )]
    public abstract class CrisBackgroundDIContainerDefinition : DIContainerDefinition<CrisBackgroundDIContainerDefinition.Data>
    {
        /// <summary>
        /// Scoped data of the <see cref="CrisBackgroundDIContainerDefinition"/>.
        /// </summary>
        public sealed class Data : BackendScopedData
        {
            [AllowNull]
            internal CrisJob _job;

            internal Data( AmbientServiceHub? ambientServiceHub )
                : base( ambientServiceHub ) 
            {
            }
        }

        /// <summary>
        /// Configures the internal services that supports background command execution.
        /// </summary>
        /// <param name="services">The services to configure.</param>
        /// <param name="scopeData">Accessor to the current scoped data.</param>
        /// <param name="globalServiceExists">Provides a way to detect if a service is available. (Unused.)</param>
        public override void ConfigureContainerServices( IServiceCollection services,
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
