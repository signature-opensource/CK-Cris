using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CK.Cris.HttpSender
{
    internal static class RetryAfterHelper
    {
        /// <summary>
        /// Parses Retry-After value from the relevant HTTP response header.
        /// If not found then it will return <see cref="TimeSpan.Zero" />.
        /// </summary>
        /// <see href="https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Retry-After" />.
        internal static bool TryParse( HttpResponseMessage response, ISystemClock timeProvider, out TimeSpan retryAfter )
        {
            var (parsed, delay) = response.Headers.RetryAfter switch
            {
                { Date: { } date } => (true, date - timeProvider.UtcNow ),
                { Delta: { } delta } => (true, delta),
                _ => (false, default)
            };

            // It can happen that the server returns a point in time in the past.
            // This indicates that retry can happen immediately.
            if( parsed && delay < TimeSpan.Zero )
            {
                delay = TimeSpan.Zero;
            }

            retryAfter = delay;
            return parsed;
        }
    }
}
