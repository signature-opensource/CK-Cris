using CK.Core;
using Polly.Timeout;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CK.Cris.HttpSender
{
    // Waiting for .NET 8 

    internal static class HttpClientResiliencePredicates
    {
        /// <summary>
        /// Determines whether an exception should be treated by resilience strategies as a transient failure.
        /// </summary>
        public static readonly Predicate<Exception> IsTransientHttpException = exception =>
        {
            Throw.CheckNotNullArgument( exception );

            return exception is HttpRequestException ||
                   exception is TimeoutRejectedException;
        };

        /// <summary>
        /// Determines whether a response contains a transient failure.
        /// </summary>
        /// <remarks> The current handling implementation uses approach proposed by Polly:
        /// <see href="https://github.com/App-vNext/Polly.Extensions.Http/blob/master/src/Polly.Extensions.Http/HttpPolicyExtensions.cs"/>.
        /// </remarks>
        public static readonly Predicate<HttpResponseMessage> IsTransientHttpFailure = response =>
        {
            Throw.CheckNotNullArgument( response );

            var statusCode = (int)response.StatusCode;

            return statusCode >= InternalServerErrorCode ||
                response.StatusCode == HttpStatusCode.RequestTimeout ||
                statusCode == TooManyRequests;

        };

        /// <summary>
        /// Determines whether an outcome should be treated by resilience strategies as a transient failure.
        /// </summary>
        public static readonly Predicate<Outcome<HttpResponseMessage>> IsTransientHttpOutcome = outcome => outcome switch
        {
            { Result: { } response } when IsTransientHttpFailure( response ) => true,
            { Exception: { } exception } when IsTransientHttpException( exception ) => true,
            _ => false
        };

        private const int InternalServerErrorCode = (int)HttpStatusCode.InternalServerError;

        private const int TooManyRequests = 429;
    }
}
