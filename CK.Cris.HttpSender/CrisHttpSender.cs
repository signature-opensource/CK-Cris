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
    public sealed partial class CrisHttpSender
    {
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
                                 HttpRetryStrategyOptions? retryStrategy )
        {
            HttpMessageHandler handler = new HttpClientHandler();
            if( retryStrategy != null )
            {
                var resilienceBuilder = new ResiliencePipelineBuilder<HttpResponseMessage>()
                                            .AddRetry( retryStrategy );
                handler = new ResilienceHandler( message => resilienceBuilder.Build() ) { InnerHandler = handler };
            }
            _httpClient = new HttpClient( handler );
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
