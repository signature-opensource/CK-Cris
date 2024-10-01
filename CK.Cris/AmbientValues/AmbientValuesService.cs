using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.AmbientValues
{
    /// <summary>
    /// Sealed service that sole purpose is to create a default <see cref="IAmbientValues"/> result:
    /// it is up to <see cref="CommandPostHandlerAttribute"/> methods to collect the values.
    /// </summary>
    public sealed class AmbientValuesService : IAutoService
    {
        readonly IPocoFactory<IAmbientValues> _factory;

        /// <summary>
        /// Initializes a new <see cref="AmbientValuesService"/>.
        /// </summary>
        /// <param name="factory">The factory of ambient values.</param>
        public AmbientValuesService( IPocoFactory<IAmbientValues> factory ) => _factory = factory;

        /// <summary>
        /// Creates the empty <see cref="IAmbientValues"/> result.
        /// Any number of <see cref="CommandPostHandlerAttribute"/> populate it.
        /// </summary>
        /// <param name="cmd">The collect command.</param>
        /// <returns>The ambient values (initially empty).</returns>
        [CommandHandler]
        public IAmbientValues GetValues( IAmbientValuesCollectCommand cmd ) => _factory.Create();
    }
}
