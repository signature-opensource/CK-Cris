using CK.Core;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Cris.AspNet
{
    /// <summary>
    /// Extends Poco types.
    /// </summary>
    public static class PocoFactoryExtensions
    {
        /// <summary>
        /// Creates a <see cref="IAspNetCrisResultError"/> from a <see cref="ICrisResultError"/>.
        /// </summary>
        /// <param name="this">This factory.</param>
        /// <param name="error">The source error result.</param>
        /// <returns>A simple error result.</returns>
        [return: NotNullIfNotNull( nameof( error ) )]
        public static CrisAspNetService.IAspNetCrisResultError? Create( this IPocoFactory<CrisAspNetService.IAspNetCrisResultError> @this, ICrisResultError? error )
        {
            if( error == null ) return null;
            var r = @this.Create();
            r.IsValidationError = error.IsValidationError;
            r.LogKey = error.LogKey;
            foreach( var m in error.Messages ) r.Messages.Add( m );
            return r;
        }

        /// <summary>
        /// Creates a <see cref="IAspNetCrisResultError"/> with at least one message.
        /// There can be no <see cref="UserMessageLevel.Error"/> messages in the messages: this result is still an error.
        /// </summary>
        /// <param name="this">This factory.</param>
        /// <param name="first">The required first message. Must be <see cref="SimpleUserMessage.IsValid"/>.</param>
        /// <param name="others">Optional other messages.</param>
        /// <returns>An error result.</returns>
        public static CrisAspNetService.IAspNetCrisResultError Create( this IPocoFactory<CrisAspNetService.IAspNetCrisResultError> @this, SimpleUserMessage first, params SimpleUserMessage[] others )
        {
            Throw.CheckArgument( first.IsValid );
            var r = @this.Create();
            r.Messages.Add( first );
            r.Messages.AddRange( others );
            return r;
        }

    }
}
