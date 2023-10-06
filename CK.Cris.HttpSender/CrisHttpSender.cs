using CK.AppIdentity;
using CK.Core;
using Microsoft.IO;
using System;
using System.Buffers;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;
using System.Net;
using static CK.Core.CheckedWriteStream;
using System.Threading;

namespace CK.Cris.HttpSender
{
    public sealed class CrisHttpSender
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

        internal static HttpRetryStrategyOptions CreateRetryStrategy( IActivityMonitor monitor, ImmutableConfigurationSection section )
        {
            return CreateRetryStrategy( 
                section.TryLookupIntValue( monitor, nameof( HttpRetryStrategyOptions.MaxRetryAttempts ), 1, int.MaxValue ), DelayBackoffType ? backoffType, bool ? useJitter, TimeSpan ? delay, TimeSpan ? maxDelay, bool ? shouldRetryAfterHeader );
        }

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

        readonly HttpClient _httpClient;
        readonly IRemoteParty _remote;
        readonly Uri _endpointUrl;
        readonly PocoDirectory _pocoDirectory;

        static MCString? _protocolErrorMsg;
        static UserMessage ProtocolErrorMessage => new UserMessage( UserMessageLevel.Error,
                                                                    _protocolErrorMsg ??= MCString.CreateNonTranslatable(
                                                                        NormalizedCultureInfo.CodeDefault,
                                                                        "Protocol error." ) );

        static MCString? _internalErrorMsg;
        static UserMessage InternalErrorMessage => new UserMessage( UserMessageLevel.Error,
                                                                    _internalErrorMsg ??= MCString.CreateNonTranslatable(
                                                                        NormalizedCultureInfo.CodeDefault,
                                                                        "Internal error." ) );

        internal CrisHttpSender( IRemoteParty remote,
                                 Uri endpointUrl,
                                 PocoDirectory pocoDirectory,
                                 int? maxRetryAttempts,
                                 DelayBackoffType? backoffType,
                                 bool? useJitter,
                                 TimeSpan? delay,
                                 TimeSpan? maxDelay,
                                 bool? shouldRetryAfterHeader )
        {
            HttpRetryStrategyOptions retry = CreateRetryStrategy( maxRetryAttempts, backoffType, useJitter, delay, maxDelay, shouldRetryAfterHeader );

            var resilienceBuilder = new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry( retry );

            var h = new ResilienceHandler( message => _defaultResilienceBuilder.Build() )
            {
                InnerHandler = new HttpClientHandler()
            };
            _httpClient = new HttpClient( h );
            _remote = remote;
            _endpointUrl = endpointUrl;
            _pocoDirectory = pocoDirectory;
        }

        /// <summary>
        /// Sends a Cris command on a remote endpoint, and returns the result.
        /// This never throws.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to send.</param>
        /// <returns>The <see cref="IExecutedCommand"/>.</returns>
        public async Task<IExecutedCommand<T>> SendAsync<T>( IActivityMonitor monitor,
                                                             T command,
                                                             CancellationToken cancellationToken = default,
                                                             [CallerLineNumber] int lineNumber = 0,
                                                             [CallerFilePath] string? fileName = null )
            where T : class, IAbstractCommand
        {
            byte[]? payloadResponse = null;
            string? payloadString = null;
            try
            {
                using var payload = (RecyclableMemoryStream)Util.RecyclableStreamManager.GetStream();
                using( var wPayload = new Utf8JsonWriter( (IBufferWriter<byte>)payload ) )
                {
                    command.Write( wPayload );
                }
                monitor.Info( CrisDirectory.CrisTag, $"Sending {(payloadString = Encoding.UTF8.GetString(payload.GetReadOnlySequence()))} to '{_remote.FullName}'.", lineNumber, fileName );
                using var request = new HttpRequestMessage( HttpMethod.Post, _endpointUrl );

                payload.Position = 0;
                request.Content = new StreamContent( payload );
                var ctx = ResilienceContextPool.Shared.Get( false, cancellationToken );
                try
                {
                    ctx.Properties.Set( _contextKey, new CallContext( this, monitor ) );
                    request.SetResilienceContext( ctx );
                    using var response = await _httpClient.SendAsync( request ).ConfigureAwait( false );
                    response.EnsureSuccessStatusCode();
                    payloadResponse = await response.Content.ReadAsByteArrayAsync().ConfigureAwait( false );
                }
                finally
                {
                    ResilienceContextPool.Shared.Return( ctx );
                }

                var crisResult = ReadAspNetCrisResult( monitor, _pocoDirectory, payloadResponse );
                if( crisResult.HasValue )
                {
                    return new ExecutedCommand<T>( command, crisResult.Value.Result, null );
                }
                var protocolError = _pocoDirectory.Create<ICrisResultError>( e => e.Messages.Add( ProtocolErrorMessage ) );
                return new ExecutedCommand<T>( command, protocolError, null );
            }
            catch( Exception ex )
            {
                payloadString ??= command.ToString();
                var errorPayloadResponse = payloadResponse != null
                                ? $"{Environment.NewLine}Response:{Environment.NewLine}{Encoding.UTF8.GetString( payloadResponse )}"
                                : null;
                monitor.Error( CrisDirectory.CrisTag, $"While sending: {payloadString}{errorPayloadResponse}", ex );
                var internalError = _pocoDirectory.Create<ICrisResultError>( e => e.Messages.Add( InternalErrorMessage ) );
                return new ExecutedCommand<T>( command, internalError, null );
            }

            static (object? Result, string? CorrelationId)? ReadAspNetCrisResult( IActivityMonitor monitor,
                                                                                  PocoDirectory pocoDirectory,
                                                                                  ReadOnlySpan<byte> payload )
            {
                var reader = new Utf8JsonReader( payload );
                Throw.DebugAssert( reader.TokenType == JsonTokenType.None );

                if( reader.Read()
                    && reader.TokenType == JsonTokenType.StartObject
                    && reader.Read()
                    && reader.TokenType == JsonTokenType.PropertyName
                    && reader.ValueTextEquals( "result" )
                    && reader.Read() )
                {
                    // TODO: expose the internal generated object? ReadObject( ref reader ) on PocoDirectory!
                    object? result;
                    switch( reader.TokenType )
                    {
                        case JsonTokenType.Null:
                            reader.Read();
                            result = null;
                            break;
                        case JsonTokenType.String:
                            result = reader.GetString();
                            reader.Read();
                            break;
                        case JsonTokenType.Number:
                            result = reader.GetDouble();
                            reader.Read();
                            break;
                        case JsonTokenType.False:
                            result = false;
                            reader.Read();
                            break;
                        case JsonTokenType.True:
                            result = true;
                            reader.Read();
                            break;
                        default:
                            result = pocoDirectory.Read( ref reader );
                            break;
                    }
                    if( reader.TokenType == JsonTokenType.PropertyName && reader.Read() )
                    {
                        return (result, reader.GetString());
                    }
                }
                monitor.Error( $"Unable to read Cris result from:{Environment.NewLine}{Encoding.UTF8.GetString(payload)}" );
                return null;
            }
        }


        /// <summary>
        /// Avoid exposing any Dispose() like method to the developper.
        /// </summary>
        internal void TearDown()
        {
            _httpClient.Dispose();
        }
    }
}
