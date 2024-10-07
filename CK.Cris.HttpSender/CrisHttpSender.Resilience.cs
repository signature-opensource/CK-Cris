using CK.Core;
using Polly;
using Polly.Retry;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace CK.Cris.HttpSender;

public sealed partial class CrisHttpSender
{
    sealed record class CallContext( CrisHttpSender Sender, IActivityMonitor Monitor );
    static ResiliencePropertyKey<CallContext> _contextKey = new ResiliencePropertyKey<CallContext>( nameof( CrisHttpSender ) );

    static ValueTask OnRetryAsync( OnRetryArguments<HttpResponseMessage> args )
    {
        if( args.Context.Properties.TryGetValue( _contextKey, out var ctx ) )
        {
            var outcome = args.Outcome;
            if( outcome.Result != null )
            {
                ctx.Monitor.Warn( CrisDirectory.CrisTag, $"Request failed on '{ctx.Sender._remote}' (attempt n°{args.AttemptNumber}): request ended with {(int)outcome.Result.StatusCode} {outcome.Result.StatusCode}." );
            }
            else
            {
                ctx.Monitor.Warn( CrisDirectory.CrisTag, $"Request failed on '{ctx.Sender._remote}' (attempt n°{args.AttemptNumber}).", outcome.Exception );
            }
        }
        return default;
    }

    internal static HttpRetryStrategyOptions CreateRetryStrategy( IActivityMonitor monitor, ImmutableConfigurationSection? section )
    {
        return section == null
                    ? new HttpRetryStrategyOptions() { OnRetry = OnRetryAsync }
                    : CreateRetryStrategy(
                        section.TryGetIntValue( monitor, nameof( HttpRetryStrategyOptions.MaxRetryAttempts ), 1, int.MaxValue ),
                        section.TryGetEnumValue<DelayBackoffType>( monitor, nameof( HttpRetryStrategyOptions.BackoffType ) ),
                        section.TryGetBooleanValue( monitor, nameof( HttpRetryStrategyOptions.UseJitter ) ),
                        section.TryGetTimeSpanValue( monitor, nameof( HttpRetryStrategyOptions.Delay ), TimeSpan.Zero, TimeSpan.FromDays( 1 ) ),
                        section.TryGetTimeSpanValue( monitor, nameof( HttpRetryStrategyOptions.MaxDelay ), TimeSpan.Zero, TimeSpan.FromDays( 1 ) ),
                        section.TryGetBooleanValue( monitor, nameof( HttpRetryStrategyOptions.ShouldRetryAfterHeader ) ) );

        static HttpRetryStrategyOptions CreateRetryStrategy( int? maxRetryAttempts, DelayBackoffType? backoffType, bool? useJitter, TimeSpan? delay, TimeSpan? maxDelay, bool? shouldRetryAfterHeader )
        {
            var retry = new HttpRetryStrategyOptions() { OnRetry = OnRetryAsync };
            if( maxRetryAttempts.HasValue ) retry.MaxRetryAttempts = maxRetryAttempts.Value;
            if( backoffType.HasValue ) retry.BackoffType = backoffType.Value;
            if( useJitter.HasValue ) retry.UseJitter = useJitter.Value;
            if( delay.HasValue ) retry.Delay = delay.Value;
            if( maxDelay.HasValue ) retry.MaxDelay = maxDelay.Value;
            if( shouldRetryAfterHeader.HasValue ) retry.ShouldRetryAfterHeader = shouldRetryAfterHeader.Value;
            return retry;
        }

    }

}
