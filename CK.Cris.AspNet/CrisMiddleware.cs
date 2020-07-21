using CK.Core;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CK.Cris.AspNet
{
    public class CrisMiddleware
    {
        static readonly PathString _crisPath = new PathString( "/.cris" );
        readonly RequestDelegate _next;
        readonly CrisAspNetService _service;

        public CrisMiddleware( RequestDelegate next, CrisAspNetService service )
        {
            if( next == null ) throw new ArgumentNullException( nameof( next ) );
            _next = next;
            _service = service;
        }

        /// <summary>
        /// Handles the command on "/.cris" path.
        /// </summary>
        /// <param name="ctx">The current context.</param>
        /// <param name="m">The request scoped monitor.</param>
        /// <returns>The awaitable.</returns>
        public async Task Invoke( HttpContext ctx, IActivityMonitor m )
        {
            PathString remainder;
            if( ctx.Request.Path.StartsWithSegments( _crisPath, out remainder ) )
            {
                if( !HttpMethods.IsPost( ctx.Request.Method ) )
                {
                    ctx.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                }
                else
                {

                }
            }
            await _next( ctx );
        }

    }
}
