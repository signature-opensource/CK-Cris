using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Cris.AspNet
{
    public class CrisMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly CrisAspNetService _service;

        public CrisMiddleware( RequestDelegate next, CrisAspNetService service )
        {
            if( next == null ) throw new ArgumentNullException( nameof( next ) );
            _next = next;
            _service = service;
        }

    }
}
