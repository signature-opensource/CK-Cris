using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.EndpointValues
{
    /// <summary>
    /// Sealed service that sole purpose is to create an empty <see cref="IEndpointValues"/> result:
    /// it is up to <see cref="CommandPostHandlerAttribute"/> methods to collect the values.
    /// </summary>
    public sealed class EndpointValuesService : IAutoService
    {
        readonly IPocoFactory<IEndpointValues> _factory;

        public EndpointValuesService( IPocoFactory<IEndpointValues> factory ) => _factory = factory;

        /// <summary>
        /// Creates the empty <see cref="IEndpointValues"/> result.
        /// Any number of <see cref="CommandPostHandlerAttribute"/> populate it.
        /// </summary>
        /// <param name="cmd">The collect command.</param>
        /// <returns>The ambient values (initially empty).</returns>
        [CommandHandler]
        public IEndpointValues GetValues( IEndpointValuesCollectCommand cmd ) => _factory.Create();
    }
}
