using CK.Core;
using FluentAssertions;
using System.Net.Http;
using System.Threading.Tasks;

namespace CK.Cris.AspNet.Tests;

static class LocalHelper
{
    public const string CrisUri = "/.cris/net";

    static public async Task<IAspNetCrisResult> GetCrisResultAsync( this PocoDirectory p, HttpResponseMessage r )
    {
        var result = p.Find<IAspNetCrisResult>()!.ReadJson( await r.Content.ReadAsByteArrayAsync() );
        Throw.DebugAssert( result != null );
        return result;
    }

    static public async Task<IAspNetCrisResult> GetCrisResultWithCorrelationIdSetToNullAsync( this PocoDirectory p, HttpResponseMessage r )
    {
        var result = await GetCrisResultAsync( p, r );
        result.CorrelationId.Should().NotBeNullOrWhiteSpace();
        result.CorrelationId = null;
        return result;
    }

}
