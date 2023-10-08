using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Cris.HttpSender
{
    // Waiting for .NET 8 
    internal sealed class ResilienceHandler : DelegatingHandler
    {
        public static readonly ResiliencePropertyKey<HttpRequestMessage> RequestMessage = new( "Resilience.Http.RequestMessage" );

        private readonly Func<HttpRequestMessage, ResiliencePipeline<HttpResponseMessage>> _pipelineProvider;

        public ResilienceHandler( Func<HttpRequestMessage, ResiliencePipeline<HttpResponseMessage>> pipelineProvider )
        {
            _pipelineProvider = pipelineProvider;
        }

        protected override async Task<HttpResponseMessage> SendAsync( HttpRequestMessage request, CancellationToken cancellationToken )
        {
            var pipeline = _pipelineProvider( request );
            var created = false;
            if( request.GetResilienceContext() is not ResilienceContext context )
            {
                context = ResilienceContextPool.Shared.Get( cancellationToken );
                created = true;
                request.SetResilienceContext( context );
            }

            context.Properties.Set( RequestMessage, request );

            try
            {
                var outcome = await pipeline.ExecuteOutcomeAsync(
                    static async ( context, state ) =>
                    {
                        var request = context.Properties.GetValue( RequestMessage, state.request );
                        try
                        {
                            var response = await state.instance.SendCoreAsync( request, context.CancellationToken ).ConfigureAwait( context.ContinueOnCapturedContext );
                            return Outcome.FromResult( response );
                        }
                        catch( Exception e )
                        {
                            return Outcome.FromException<HttpResponseMessage>( e );
                        }
                    },
                    context,
                    (instance: this, request) )
                    .ConfigureAwait( context.ContinueOnCapturedContext );

                outcome.ThrowIfException();

                return outcome.Result!;
            }
            finally
            {
                if( created )
                {
                    ResilienceContextPool.Shared.Return( context );
                    request.SetResilienceContext( null );
                }
            }
        }

        private Task<HttpResponseMessage> SendCoreAsync( HttpRequestMessage requestMessage, CancellationToken cancellationToken )
            => base.SendAsync( requestMessage, cancellationToken );
    }
}
