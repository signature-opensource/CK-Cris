using CK.AppIdentity;
using CK.Core;
using Polly;
using System;

namespace CK.Cris.HttpSender;

/// <summary>
/// The configuration of the "CrisHttpSender" feature of a remote (<see cref="CrisHttpSenderFeatureDriver"/>).
/// <code>
/// "CrisHttpSender": { "Timeout": "00:01:00", "DisableServerCertificateValidation": false, "Retry": true }
/// </code>
/// Its "Retry" is its option (<see cref="CrisHttpSenderRetryOption"/>): <c>true</c> or its section enables it.
/// </summary>
[ConfigurationModel]
public sealed class CrisHttpSenderConfiguration : IConfigurationModel
{
    /// <summary>
    /// Gets the timeout of a request (100 seconds by default).
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets whether the certificate of the server is not validated (for a development server only).
    /// </summary>
    public bool DisableServerCertificateValidation { get; init; }

    /// <inheritdoc />
    public void Validate( IConfigurationValidationContext context )
    {
        if( Timeout is { } t && t <= TimeSpan.Zero ) context.Error( $"'{context.Path}:Timeout' is {t}: a positive duration is expected." );
    }
}

/// <summary>
/// The "Retry" option of the "CrisHttpSender" feature: the failed requests are retried (exponential backoff by default).
/// Its section changes the defaults of the strategy.
/// <code>
/// "CrisHttpSender": { "Retry": { "MaxRetryAttempts": 5, "Delay": "00:00:01" } }
/// </code>
/// </summary>
[PartyFeatureOption( "Retry", TypeActivation.Optional )]
[Configuration<CrisHttpSenderRetryConfiguration>]
public sealed class CrisHttpSenderRetryOption : IPartyComponentOption<CrisHttpSenderFeatureDriver>
{
    /// <summary>
    /// Initializes a new <see cref="CrisHttpSenderRetryOption"/>.
    /// </summary>
    /// <param name="configuration">Its configuration.</param>
    public CrisHttpSenderRetryOption( CrisHttpSenderRetryConfiguration configuration )
    {
        Configuration = configuration;
    }

    /// <summary>
    /// Gets the configuration of the retry strategy.
    /// </summary>
    public CrisHttpSenderRetryConfiguration Configuration { get; }
}

/// <summary>
/// The configuration of the "Retry" option of the "CrisHttpSender" feature (<see cref="CrisHttpSenderRetryOption"/>): an
/// absent key keeps the default of the strategy.
/// </summary>
[ConfigurationModel]
public sealed class CrisHttpSenderRetryConfiguration : IConfigurationModel
{
    /// <summary>
    /// Gets the maximum number of retries.
    /// </summary>
    public int? MaxRetryAttempts { get; init; }

    /// <summary>
    /// Gets the type of the backoff.
    /// </summary>
    public DelayBackoffType? BackoffType { get; init; }

    /// <summary>
    /// Gets whether a jitter is added to the delay.
    /// </summary>
    public bool? UseJitter { get; init; }

    /// <summary>
    /// Gets the base delay between retries.
    /// </summary>
    public TimeSpan? Delay { get; init; }

    /// <summary>
    /// Gets the maximum delay between retries.
    /// </summary>
    public TimeSpan? MaxDelay { get; init; }

    /// <summary>
    /// Gets whether the "Retry-After" header of the response is honored.
    /// </summary>
    public bool? ShouldRetryAfterHeader { get; init; }

    /// <inheritdoc />
    public void Validate( IConfigurationValidationContext context )
    {
        if( MaxRetryAttempts is < 1 ) context.Error( $"'{context.Path}:MaxRetryAttempts' is {MaxRetryAttempts}: a positive integer is expected." );
        Check( context, nameof( Delay ), Delay );
        Check( context, nameof( MaxDelay ), MaxDelay );

        static void Check( IConfigurationValidationContext context, string name, TimeSpan? t )
        {
            if( t is { } v && (v < TimeSpan.Zero || v > TimeSpan.FromDays( 1 )) ) context.Error( $"'{context.Path}:{name}' is {v}: between 0 and 1 day is expected." );
        }
    }
}
