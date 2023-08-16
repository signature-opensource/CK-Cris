using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Extends Poco types.
    /// </summary>
    public static class PocoFactoryExtensions
    {
        /// <summary>
        /// Creates a <see cref="ICrisResultError"/> with at least one error.
        /// </summary>
        /// <param name="this">This factory.</param>
        /// <param name="first">The required first message. Must be <see cref="ResultMessage.IsValid"/>.</param>
        /// <param name="others">Optional other messages.</param>
        /// <returns>An error result.</returns>
        public static ICrisResultError Create( this IPocoFactory<ICrisResultError> @this, ResultMessage first, params ResultMessage[] others )
        {
            Throw.CheckArgument( first.IsValid );
            var r = @this.Create();
            r.UserMessages.Add( first );
            r.UserMessages.AddRange( others );
            return r;
        }
    }
}
