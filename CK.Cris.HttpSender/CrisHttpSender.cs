using CK.AppIdentity;
using CK.Core;
using Microsoft.IO;
using System;
using System.Buffers;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Polly;
using System.Threading;
using System.Linq;

namespace CK.Cris.HttpSender
{
    /// <summary>
    /// Supports sending Cris commands to "<see cref="IRemoteParty.Address"/>/.cris/net" endpoint.
    /// The remote address must be a "http://" or "https://" address without any path or query part.
    /// </summary>
    public sealed partial class CrisHttpSender : ICrisHttpSender
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
                                 TimeSpan? timeout,
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
            _httpClient.Timeout = timeout ?? TimeSpan.FromMinutes( 1 );
            _remote = remote;
            _endpointUrl = endpointUrl;
            _pocoDirectory = pocoDirectory;
        }

        /// <inheritdoc />
        public IRemoteParty Remote => _remote;

        /// <inheritdoc />
        public Task<IExecutedCommand<T>> SendAsync<T>( IActivityMonitor monitor,
                                                       T command,
                                                       CancellationToken cancellationToken = default,
                                                       [CallerLineNumber] int lineNumber = 0,
                                                       [CallerFilePath] string? fileName = null )
            where T : class, IAbstractCommand
        {
            return DoSendAsync( monitor, command, throwError: false, lineNumber, fileName, cancellationToken );
        }

        /// <inheritdoc />
        public async Task<IExecutedCommand<T>> SendOrThrowAsync<T>( IActivityMonitor monitor,
                                                                    T command,
                                                                    CancellationToken cancellationToken = default,
                                                                    [CallerLineNumber] int lineNumber = 0,
                                                                    [CallerFilePath] string? fileName = null )
            where T : class, IAbstractCommand
        {
            var r = await DoSendAsync( monitor, command, throwError: true, lineNumber, fileName, cancellationToken ).ConfigureAwait( false );
            if( r.Result is ICrisResultError e )
            {
                throw e.CreateException( lineNumber, fileName );
            }
            return r;
        }

        /// <inheritdoc />
        public async Task<TResult> SendAndGetResultOrThrowAsync<TResult>( IActivityMonitor monitor,
                                                                          ICommand<TResult> command,
                                                                          CancellationToken cancellationToken = default,
                                                                          [CallerLineNumber] int lineNumber = 0,
                                                                          [CallerFilePath] string? fileName = null )
        {
            var r = await SendOrThrowAsync( monitor, command, cancellationToken, lineNumber, fileName ).ConfigureAwait( false );
            return r.WithResult<TResult>().Result;
        }

        async Task<IExecutedCommand<T>> DoSendAsync<T>( IActivityMonitor monitor,
                                                        T command,
                                                        bool throwError,
                                                        int lineNumber,
                                                        string? fileName,
                                                        CancellationToken cancellationToken )
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
                monitor.Info( CrisDirectory.CrisTag, $"Sending {(payloadString = Encoding.UTF8.GetString( payload.GetReadOnlySequence() ))} to '{_remote.FullName}'.", lineNumber, fileName );
                using var request = new HttpRequestMessage( HttpMethod.Post, _endpointUrl );

                payload.Position = 0;
                request.Content = new StreamContent( payload );
                var ctx = ResilienceContextPool.Shared.Get( false, cancellationToken );
                try
                {
                    ctx.Properties.Set( _contextKey, new CallContext( this, monitor ) );
                    request.SetResilienceContext( ctx );
                    using var response = await _httpClient.SendAsync( request, cancellationToken ).ConfigureAwait( false );
                    response.EnsureSuccessStatusCode();
                    payloadResponse = await response.Content.ReadAsByteArrayAsync( cancellationToken ).ConfigureAwait( false );
                }
                finally
                {
                    ResilienceContextPool.Shared.Return( ctx );
                }

                var crisResult = ReadAspNetCrisResult( monitor, _pocoDirectory, payloadResponse, throwError );
                if( crisResult.HasValue )
                {
                    return new ExecutedCommand<T>( command, crisResult.Value.Result, null );
                }
                var protocolError = _pocoDirectory.Create<ICrisResultError>( e => e.Messages.Add( ProtocolErrorMessage ) );
                return new ExecutedCommand<T>( command, protocolError, null );
            }
            catch( Exception ex )
            {
                if( throwError ) throw;
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
                                                                                  ReadOnlySpan<byte> payload,
                                                                                  bool throwError )
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
                    // TODO: expose the internal generated object? ReadAny( ref reader ) on PocoDirectory!
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
                var msg = $"Unable to read Cris result from:{Environment.NewLine}{Encoding.UTF8.GetString( payload )}";
                if( throwError ) throw new CKException( msg );
                monitor.Error( msg );
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
