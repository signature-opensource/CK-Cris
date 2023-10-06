using CK.AppIdentity;
using CK.Core;
using Microsoft.Extensions.Configuration;
using Polly.Retry;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Cris.HttpSender
{
    /// <summary>
    /// This feature applies only to <see cref="IRemoteParty"/> that may be <see cref="IRemoteParty.IsExternalParty"/>
    /// or not. The <see cref="CrisHttpSender"/> feature is added only if a "CrisHttpSender" key appears in the remote's
    /// configuration. This configuration can be a "true" value (that uses a default configuration).
    /// </summary>
    public sealed class CrisHttpSenderFeatureDriver : ApplicationIdentityFeatureDriver
    {
        readonly PocoDirectory _pocoDirectory;

        /// <summary>
        /// Initializes a new <see cref="CrisHttpSenderFeatureDriver"/>.
        /// </summary>
        /// <param name="s">The application identity service.</param>
        /// <param name="pocoDirectory">The poco directory.</param>
        public CrisHttpSenderFeatureDriver( ApplicationIdentityService s, PocoDirectory pocoDirectory )
            : base( s, isAllowedByDefault: true )
        {
            _pocoDirectory = pocoDirectory;
        }

        protected override Task<bool> SetupAsync( FeatureLifetimeContext context )
        {
            bool success = true;
            foreach( var r in context.GetAllRemotes().Where( r => IsAllowedFeature( r ) ) )
            {
                success &= PlugFeature( context.Monitor, r );
            }
            return Task.FromResult( success );
        }

        protected override Task<bool> SetupDynamicRemoteAsync( FeatureLifetimeContext context, IOwnedParty party )
        {
            // Since we only use the GetAllRemotes helper, SetupAsync does the job.
            return SetupAsync( context );
        }

        protected override Task TeardownAsync( FeatureLifetimeContext context )
        {
            foreach( var r in context.GetAllRemotes() )
            {
                // Feature instances cannot be removed, they must just be torn down.
                var f = r.GetFeature<CrisHttpSender>();
                f?.TearDown();
            }
            return Task.CompletedTask;
        }

        protected override Task TeardownDynamicRemoteAsync( FeatureLifetimeContext context, IOwnedParty party )
        {
            // Since we only use the GetAllRemotes helper, TeardownAsync does the job.
            return TeardownAsync( context );
        }

        static bool HasConfig( IConfigurationSection section, out bool isSection )
        {
            if( bool.TryParse( section.Value, out var b ) )
            {
                if( b )
                {
                    isSection = false;
                    return true;
                }
                isSection = false;
                return false;
            }
            Throw.DebugAssert( section.GetChildren().Any() );
            isSection = true;
            return true;
        }

        bool PlugFeature( IActivityMonitor monitor, IRemoteParty r )
        {
            var config = r.Configuration.Configuration.TryGetSection( "CrisHttpSender" );
            // Allow "CrisHttpSender" = "false" or "true".
            // This supports default, empty, configuration.
            if( config != null && HasConfig( config, out var isSection ) )
            {
                if( r.Address is null
                    || !Uri.TryCreate( r.Address, UriKind.Absolute, out var uri )
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) )
                {
                    monitor.Error( $"Unable to setup feature 'CrisHttpSender' on '{r.FullName}': Address '{r.Address}' must be a valid http:// or https:// url." );
                    return false;
                }
                if( uri.PathAndQuery != "/" )
                {
                    monitor.Error( $"Unable to setup feature 'CrisHttpSender' on '{r.FullName}': Address '{r.Address}' url must not have a path and/or a query part." );
                    return false;
                }
                var retryStrategy = CrisHttpSender.CreateRetryStrategy( monitor, isSection ? config : null );
                monitor.Info( $"Enabling 'CrisHttpSender' on '{r}' with address '{uri}'." );
                r.AddFeature( new CrisHttpSender( r, new( uri, ".cris/net"), _pocoDirectory, retryStrategy ) );
            }
            return true;
        }

    }
}
