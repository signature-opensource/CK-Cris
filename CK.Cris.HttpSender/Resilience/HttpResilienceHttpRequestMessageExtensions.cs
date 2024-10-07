using Polly;
using System.Net.Http;

namespace CK.Cris.HttpSender;

// Waiting for .NET 8 

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
