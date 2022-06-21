using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.AmbientValues
{
    /// <summary>
    /// Sealed service that sole purpose is to create an empty <see cref="IAmbientValues"/> result.
    /// </summary>
    public sealed class AmbientValuesService : IAutoService
    {
        readonly IPocoFactory<IAmbientValues> _factory;

        public AmbientValuesService( IPocoFactory<IAmbientValues> factory ) => _factory = factory;

        /// <summary>
        /// Creates the empty <see cref="IAmbientValues"/> result.
        /// Any number of <see cref="CommandPostHandlerAttribute"/> can be used to populate the it.
        /// </summary>
        /// <param name="cmd">The collect command.</param>
        /// <returns>The ambient values (initially empty).</returns>
        [CommandHandler]
        public IAmbientValues GetValues( IAmbientValuesCollectCommand cmd ) => _factory.Create();
    }
}
