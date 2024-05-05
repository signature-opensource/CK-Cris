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
using CK.Auth;
using CK.Poco.Exc.Json;
using CK.Setup;
using CK.Cris.AspNet;
using Newtonsoft.Json.Linq;

namespace CK.Cris.HttpSender
{
    /// <summary>
    /// Supports sending Cris commands to "<see cref="IRemoteParty.Address"/>/.cris/net" endpoint.
    /// The remote address must be a "http://" or "https://" address without any path or query part.
    /// </summary>
    public sealed partial class CrisHttpSender : ICrisHttpSender
    {
        readonly HttpClient _httpClient;
        readonly TokenAndTimeoutHandler _topHandler;
        readonly IRemoteParty _remote;
        readonly Uri _endpointUrl;
        readonly PocoDirectory _pocoDirectory;
        readonly IPocoFactory<IAspNetCrisResult> _resultFactory;
        readonly TimeSpan _configuredTimeout;

        static readonly HttpRequestOptionsKey<TimeSpan> _timeoutKey = new HttpRequestOptionsKey<TimeSpan>( "Timeout" );

        static MCString? _protocolErrorMsg;
        static UserMessage ProtocolErrorMessage => new UserMessage( UserMessageLevel.Error,
                                                                    _protocolErrorMsg ??= MCString.CreateNonTranslatable(
                                                                    NormalizedCultureInfo.CodeDefault,
                                                                    "Protocol error." ) );

        static MCString? _internalErrorMsg;
        private bool _skipAutomaticAuthorizationToken;

        static UserMessage InternalErrorMessage => new UserMessage( UserMessageLevel.Error,
                                                                    _internalErrorMsg ??= MCString.CreateNonTranslatable(
                                                                    NormalizedCultureInfo.CodeDefault,
                                                                    "Internal error." ) );

        internal CrisHttpSender( IRemoteParty remote,
                                 Uri endpointUrl,
                                 PocoDirectory pocoDirectory,
                                 IPocoFactory<IAspNetCrisResult> resultFactory,
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
            handler = _topHandler = new TokenAndTimeoutHandler{ InnerHandler = handler };
            _httpClient = new HttpClient( handler );
            _configuredTimeout = timeout ?? TimeSpan.FromMinutes( 1 );
            _httpClient.Timeout = Timeout.InfiniteTimeSpan;
            _remote = remote;
            _endpointUrl = endpointUrl;
            _pocoDirectory = pocoDirectory;
            _resultFactory = resultFactory;
        }

        /// <inheritdoc />
        public IRemoteParty Remote => _remote;

        public bool SkipAutomaticAuthorizationToken
        {
            get => _skipAutomaticAuthorizationToken;
            set => _skipAutomaticAuthorizationToken = value;
        }

        /// <inheritdoc />
        public string? AuthorizationToken
        {
            get => _topHandler.Token;
            set => _topHandler.Token = value;
        }

        /// <inheritdoc />
        public Task<IExecutedCommand<T>> SendAsync<T>( IActivityMonitor monitor,
                                                       T command,
                                                       TimeSpan? timeout = null,
                                                       CancellationToken cancellationToken = default,
                                                       [CallerLineNumber] int lineNumber = 0,
                                                       [CallerFilePath] string? fileName = null )
            where T : class, IAbstractCommand
        {
            return DoSendAsync( monitor, command, throwError: false, lineNumber, fileName, timeout, cancellationToken );
        }

        /// <inheritdoc />
        public async Task<IExecutedCommand<T>> SendOrThrowAsync<T>( IActivityMonitor monitor,
                                                                    T command,
                                                                    TimeSpan? timeout = null,
                                                                    CancellationToken cancellationToken = default,
                                                                    [CallerLineNumber] int lineNumber = 0,
                                                                    [CallerFilePath] string? fileName = null )
            where T : class, IAbstractCommand
        {
            var r = await DoSendAsync( monitor, command, throwError: true, lineNumber, fileName, timeout, cancellationToken ).ConfigureAwait( false );
            if( r.Result is ICrisResultError e )
            {
                throw e.CreateException( lineNumber, fileName );
            }
            return r;
        }

        /// <inheritdoc />
        public async Task<TResult> SendAndGetResultOrThrowAsync<TResult>( IActivityMonitor monitor,
                                                                          ICommand<TResult> command,
                                                                          TimeSpan? timeout = null,
                                                                          CancellationToken cancellationToken = default,
                                                                          [CallerLineNumber] int lineNumber = 0,
                                                                          [CallerFilePath] string? fileName = null )
        {
            var r = await SendOrThrowAsync( monitor, command, timeout, cancellationToken, lineNumber, fileName ).ConfigureAwait( false );
            return r.WithResult<TResult>().Result;
        }

        async Task<IExecutedCommand<T>> DoSendAsync<T>( IActivityMonitor monitor,
                                                        T command,
                                                        bool throwError,
                                                        int lineNumber,
                                                        string? fileName,
                                                        TimeSpan? timeout,
                                                        CancellationToken cancellationToken )
            where T : class, IAbstractCommand
        {
            byte[]? payloadResponse = null;
            string? payloadString = null;
            try
            {
                using var payload = (RecyclableMemoryStream)Util.RecyclableStreamManager.GetStream();
                _pocoDirectory.WriteJson( (IBufferWriter<byte>)payload, command, withType: true, PocoJsonExportOptions.Default );
                monitor.Info( CrisDirectory.CrisTag, $"Sending {(payloadString = Encoding.UTF8.GetString( payload.GetReadOnlySequence() ))} to '{_remote.FullName}'.", lineNumber, fileName );
                using var request = new HttpRequestMessage( HttpMethod.Post, _endpointUrl );

                payload.Position = 0;
                request.Content = new StreamContent( payload );
                var ctx = ResilienceContextPool.Shared.Get( false, cancellationToken );
                try
                {
                    if( !timeout.HasValue ) timeout = _configuredTimeout;
                    // Normalizes TimeSpan.MaxValue to the less known System.Threading.Timeout.InfiniteTimeSpan
                    // that is the one used by HttpClient.
                    if( timeout.Value == TimeSpan.MaxValue ) timeout = Timeout.InfiniteTimeSpan;
                    request.Options.Set( _timeoutKey, timeout.Value );

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

                IAspNetCrisResult? result = _resultFactory.ReadJson( payloadResponse, PocoJsonImportOptions.Default );
                if( result == null ) throw new Exception( "Received 'null' response." );
                if( !_skipAutomaticAuthorizationToken )
                {
                    HandleAutomaticAuthorizationToken( monitor, command, result.Result );
                }
                return new ExecutedCommand<T>( command, result.Result, events: null );
            }
            catch( Exception ex )
            {
                if( throwError ) throw;
                payloadString ??= command.ToString();
                var errorPayloadResponse = payloadResponse != null
                                ? $"{Environment.NewLine}Response:{Environment.NewLine}{Encoding.UTF8.GetString( payloadResponse )}"
                                : null;
                monitor.Error( CrisDirectory.CrisTag, $"While sending: {payloadString}{errorPayloadResponse}", ex );
                var internalError = _pocoDirectory.Create<ICrisResultError>( e => e.Errors.Add( InternalErrorMessage ) );
                return new ExecutedCommand<T>( command, internalError, events: null );
            }
        }

        void HandleAutomaticAuthorizationToken( IActivityMonitor monitor, object command, object? result )
        {
            if( result is IAuthenticationResult authResult )
            {
                if( authResult.Success && command is IBasicLoginCommand or IRefreshAuthenticationCommand )
                {
                    _topHandler.Token = authResult.Token;
                    monitor.Info( $"Updating the AuthorizationToken. User '{authResult.Info.ActualUser.UserName} ({authResult.Info.ActualUser.UserId})'." );
                }
                else
                {
                    monitor.Warn( $"A IAuthenticationResult has been received but not from a IBasicLoginCommand nor IRefreshAuthenticationCommand. " +
                                  $"Skipping AuthorizationToken update." );
                }
            }
            else if( command is ILogoutCommand )
            {
                _topHandler.Token = null;
                monitor.Info( $"Logout command succeeded: clearing AuthorizationToken." );
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
