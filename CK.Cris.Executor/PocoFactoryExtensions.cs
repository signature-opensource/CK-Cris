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
        /// Creates a <see cref="ICrisErrorResult"/> with at least one error.
        /// </summary>
        /// <param name="this">This factory.</param>
        /// <param name="firstError">The required first error. Must not be empty or whitespace.</param>
        /// <param name="otherErrors">Optional other errors (nulls are skipped).</param>
        /// <returns>An error result.</returns>
        public static ICrisErrorResult Create( this IPocoFactory<ICrisErrorResult> @this, string firstError, params string?[] otherErrors )
        {
            Throw.CheckNotNullOrWhiteSpaceArgument( firstError );
            var r = @this.Create();
            r.Errors.Add( firstError );
            r.Errors.AddRange( otherErrors.Where( e => e != null ).Select( e => e! ) );
            return r;
        }
    }
}
