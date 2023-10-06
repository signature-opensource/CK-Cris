using CK.Core;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CK.Cris.HttpSender
{
    /// <summary>
    /// The resilience extensions for <see cref="HttpRequestMessage"/>.
    /// </summary>
    public static class HttpResilienceHttpRequestMessageExtensions
    {
        private static readonly HttpRequestOptionsKey<ResilienceContext?> _resilienceContextKey = new( "Resilience.Http.ResilienceContext" );

        /// <summary>
        /// Gets the <see cref="ResilienceContext"/> from the request message.
        /// </summary>
        /// <param name="requestMessage">The request.</param>
        /// <returns>An instance of <see cref="ResilienceContext"/> or <see langword="null"/>.</returns>
        public static ResilienceContext? GetResilienceContext( this HttpRequestMessage requestMessage )
        {
            if( requestMessage.Options.TryGetValue( _resilienceContextKey, out var context ) )
            {
                return context;
            }
            return null;
        }

        /// <summary>
        /// Sets the <see cref="ResilienceContext"/> on the request message.
        /// </summary>
        /// <param name="requestMessage">The request.</param>
        /// <param name="resilienceContext">An instance of <see cref="ResilienceContext"/>.</param>
        public static void SetResilienceContext( this HttpRequestMessage requestMessage, ResilienceContext? resilienceContext )
        {
            requestMessage.Options.Set( _resilienceContextKey, resilienceContext );
        }
    }
}
