using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.AmbientValues
{
    /// <summary>
    /// Sealed service that sole purpose is to create an empty ambient values result.
    /// </summary>
    public sealed class AmbientValuesCollectHandler : IAutoService
    {
        /// <summary>
        /// Creates the empty string to object result.
        /// Any number of <see cref="CommandPostHandlerAttribute"/> can be used to populate the it.
        /// </summary>
        /// <param name="cmd">The collect command.</param>
        /// <returns>The ambient values (initially empty).</returns>
        [CommandHandler]
        public Dictionary<string, object?> GetValues( IAmbientValuesCollectCommand cmd ) => new Dictionary<string, object?>();
    }
}
