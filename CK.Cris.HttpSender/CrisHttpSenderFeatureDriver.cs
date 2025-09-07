using CK.AppIdentity;
using CK.Core;
using CK.Setup;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Cris.HttpSender;

/// <summary>
/// This feature applies only to <see cref="IRemoteParty"/> that may be <see cref="IRemoteParty.IsExternalParty"/>
/// or not. The <see cref="CrisHttpSender"/> feature is added only if a "CrisHttpSender" key appears in the remote's
/// configuration. This configuration can be a "true" value (that uses a default configuration).
/// </summary>
[AlsoRegisterType<ICrisCallResult>]
public sealed class CrisHttpSenderFeatureDriver : ApplicationIdentityFeatureDriver
{
    readonly PocoDirectory _pocoDirectory;
    readonly IPocoFactory<ICrisCallResult> _resultReader;

    /// <summary>
    /// Initializes a new <see cref="CrisHttpSenderFeatureDriver"/>.
    /// </summary>
    /// <param name="s">The application identity service.</param>
    /// <param name="pocoDirectory">The poco directory.</param>
    /// <param name="resultReader">The AspNet crist result factory.</param>
    public CrisHttpSenderFeatureDriver( ApplicationIdentityService s,
                                        PocoDirectory pocoDirectory,
                                        IPocoFactory<ICrisCallResult> resultReader )
        : base( s, isAllowedByDefault: true )
    {
        _pocoDirectory = pocoDirectory;
        _resultReader = resultReader;
    }

    /// <summary>
    /// Adds the <see cref="CrisHttpSender"/> feature to any <see cref="FeatureLifetimeContext.GetAllRemotes()"/>
    /// that has a "CrisHttpSender" section.
    /// </summary>
    /// <param name="context">The lifetime context.</param>
    /// <returns>True on success, false on error.</returns>
    protected override Task<bool> SetupAsync( FeatureLifetimeContext context )
    {
        bool success = true;
        foreach( var r in context.GetAllRemotes().Where( r => IsAllowedFeature( r ) ) )
        {
            success &= PlugFeature( context.Monitor, r );
        }
        return Task.FromResult( success );
    }

    /// <summary>
    /// Adds the <see cref="CrisHttpSender"/> feature to any <see cref="FeatureLifetimeContext.GetAllRemotes()"/>
    /// that has a "CrisHttpSender" section.
    /// </summary>
    /// <param name="context">The lifetime context.</param>
    /// <param name="party">The dynamic party to initialize.</param>
    /// <returns>True on success, false on error.</returns>
    protected override Task<bool> SetupDynamicRemoteAsync( FeatureLifetimeContext context, IOwnedParty party )
    {
        // Since we only use the GetAllRemotes helper, SetupAsync does the job.
        return SetupAsync( context );
    }

    /// <summary>
    /// Tears down the <see cref="CrisHttpSender"/> feature (by disposing its HttpClient)
    /// from any <see cref="FeatureLifetimeContext.GetAllRemotes()"/> that has it.
    /// </summary>
    /// <param name="context">The lifetime context.</param>
    /// <returns>The awaitable.</returns>
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

    /// <summary>
    /// Tears down the <see cref="CrisHttpSender"/> feature (by disposing its HttpClient)
    /// from any <see cref="FeatureLifetimeContext.GetAllRemotes()"/> that has it.
    /// </summary>
    /// <param name="context">The lifetime context.</param>
    /// <param name="party">The dynamic party to cleanup.</param>
    /// <returns>The awaitable.</returns>
    protected override Task TeardownDynamicRemoteAsync( FeatureLifetimeContext context, IOwnedParty party )
    {
        // Since we only use the GetAllRemotes helper, TeardownAsync does the job.
        return TeardownAsync( context );
    }

    bool PlugFeature( IActivityMonitor monitor, IRemoteParty r )
    {
        // Allow "CrisHttpSender" = "false" or "true".
        // This supports default, empty, configuration.
        if( r.Configuration.Configuration.ShouldApplyConfiguration( "CrisHttpSender", optOut: false, out var config ) )
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
            HttpRetryStrategyOptions? retryStrategy = null;
            if( config.ShouldApplyConfiguration( "Retry", optOut: true, out var retryConfig ) )
            {
                retryStrategy = CrisHttpSender.CreateRetryStrategy( monitor, retryConfig );
            }
            bool disableServerCertificateValidation = false;
            TimeSpan? timeout = null;
            if( config != null )
            {
                var s = config["Timeout"];
                if( s != null )
                {
                    if( TimeSpan.TryParse( s, CultureInfo.InvariantCulture, out var tOut ) )
                    {
                        timeout = tOut;
                    }
                    else
                    {
                        monitor.Warn( $"Unable to parse CrisHttpSender Timout '{s}' for remote '{r}'. Using default value of 100 seconds." );
                    }
                }
                disableServerCertificateValidation = config.TryGetBooleanValue( monitor, "DisableServerCertificateValidation" ) ?? false;
            }
            monitor.Info( $"Enabling 'CrisHttpSender' on '{r}' with address '{uri}'." );
            r.AddFeature( new CrisHttpSender( r, new( uri, ".cris/net" ), disableServerCertificateValidation, _pocoDirectory, _resultReader, timeout, retryStrategy ) );
        }
        return true;
    }

}
