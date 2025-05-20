using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Cris.HttpSender;

public sealed partial class CrisHttpSender
{
    sealed class TokenAndTimeoutHandler : DelegatingHandler
    {
        string? _token;
        AuthenticationHeaderValue? _bearer;

        public string? Token
        {
            get => _token;
            set
            {
                if( value == null )
                {
                    _token = null;
                    _bearer = null;
                }
                else
                {
                    _token = value;
                    _bearer = new AuthenticationHeaderValue( "Bearer", value );
                }
            }
        }

        static CancellationTokenSource? CreateCTS( HttpRequestMessage request, CancellationToken userToken )
        {
            if( request.Options.TryGetValue( _timeoutKey, out var timeout ) && timeout != Timeout.InfiniteTimeSpan )
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource( userToken );
                cts.CancelAfter( timeout );
                return cts;
            }
            return null;
        }

        protected override async Task<HttpResponseMessage> SendAsync( HttpRequestMessage request, CancellationToken cancellationToken )
        {
            var cts = CreateCTS( request, cancellationToken );
            try
            {
                if( _bearer != null )
                {
                    request.Headers.Authorization = _bearer;
                }
                return await base.SendAsync( request, cts?.Token ?? cancellationToken ).ConfigureAwait( false );
            }
            catch( OperationCanceledException ) when( !cancellationToken.IsCancellationRequested )
            {
                throw new TimeoutException();
            }
            finally
            {
                cts?.Dispose();
            }
        }
    }
}
