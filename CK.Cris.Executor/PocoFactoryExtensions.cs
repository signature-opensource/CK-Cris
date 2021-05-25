using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CK.Cris
{
    public static class PocoFactoryExtensions
    {
        /// <summary>
        /// Creates a <see cref="ISimpleErrorResult"/> with at least one error.
        /// </summary>
        /// <param name="this">This factory.</param>
        /// <param name="firstError">The required first error.</param>
        /// <param name="otherErrors">Optional other errors (nulls are skipped).</param>
        /// <returns>A simple validation result.</returns>
        public static ISimpleErrorResult Create( this IPocoFactory<ISimpleErrorResult> @this, string firstError, params string?[] otherErrors )
        {
            var r = @this.Create();
            r.Errors.Add( firstError );
            r.Errors.AddRange( otherErrors.Where( e => e != null ).Select( e => e! ) );
            return r;
        }
    }
}
