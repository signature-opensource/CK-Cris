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

    internal static HttpRetryStrategyOptions CreateRetryStrategy( CrisHttpSenderRetryConfiguration c )
    {
        var retry = new HttpRetryStrategyOptions() { OnRetry = OnRetryAsync };
        if( c.MaxRetryAttempts.HasValue ) retry.MaxRetryAttempts = c.MaxRetryAttempts.Value;
        if( c.BackoffType.HasValue ) retry.BackoffType = c.BackoffType.Value;
        if( c.UseJitter.HasValue ) retry.UseJitter = c.UseJitter.Value;
        if( c.Delay.HasValue ) retry.Delay = c.Delay.Value;
        if( c.MaxDelay.HasValue ) retry.MaxDelay = c.MaxDelay.Value;
        if( c.ShouldRetryAfterHeader.HasValue ) retry.ShouldRetryAfterHeader = c.ShouldRetryAfterHeader.Value;
        return retry;
    }

}
