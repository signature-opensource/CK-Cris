using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.UbiquitousValues
{
    /// <summary>
    /// Sealed service that sole purpose is to create a default <see cref="IUbiquitousValues"/> result:
    /// it is up to <see cref="CommandPostHandlerAttribute"/> methods to collect the values.
    /// </summary>
    public sealed class UbiquitousValuesService : IAutoService
    {
        readonly IPocoFactory<IUbiquitousValues> _factory;

        public UbiquitousValuesService( IPocoFactory<IUbiquitousValues> factory ) => _factory = factory;

        /// <summary>
        /// Creates the empty <see cref="IUbiquitousValues"/> result.
        /// Any number of <see cref="CommandPostHandlerAttribute"/> populate it.
        /// </summary>
        /// <param name="cmd">The collect command.</param>
        /// <returns>The ambient values (initially empty).</returns>
        [CommandHandler]
        public IUbiquitousValues GetValues( IUbiquitousValuesCollectCommand cmd ) => _factory.Create();
    }
}
