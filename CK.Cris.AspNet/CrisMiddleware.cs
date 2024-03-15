using CK.Core;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CK.Cris.AspNet
{
    public class CrisMiddleware
    {
        static readonly PathString _crisPath = new PathString( "/.cris" );
        static readonly PathString _netPath = new PathString( "/net" );
        readonly RequestDelegate _next;
        readonly CrisAspNetService _service;

        public CrisMiddleware( RequestDelegate next, CrisAspNetService service )
        {
            Throw.CheckNotNullArgument( next );
            _next = next;
            _service = service;
        }

        /// <summary>
        /// Handles the command on "/.cris" path.
        /// </summary>
        /// <param name="ctx">The current context.</param>
        /// <param name="monitor">The request scoped monitor.</param>
        /// <returns>The awaitable.</returns>
        public async Task InvokeAsync( HttpContext ctx, IActivityMonitor monitor )
        {
            if( ctx.Request.Path.StartsWithSegments( _crisPath, out PathString remainder ) )
            {
                if( !HttpMethods.IsPost( ctx.Request.Method ) )
                {
                    ctx.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                }
                else
                {
                    ctx.Response.StatusCode = 200;
                    bool isNetPath = remainder.StartsWithSegments( _netPath );
                    var result = await _service.HandleRequestAsync( monitor, ctx.Request, CrisAspNetService.StandardReadCommandAsync, isNetPath );
                    using( var writer = new Utf8JsonWriter( ctx.Response.BodyWriter ) )
                    {
                        PocoJsonSerializer.Write( result, writer, withType: false );
                    }
                }
            }
            else
            {
                await _next( ctx );
            }
        }

    }
}
