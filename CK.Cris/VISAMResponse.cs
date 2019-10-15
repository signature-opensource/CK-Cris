using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Simple encapsulation of Cris response.
    /// </summary>
    public class VISAMResponse
    {
        VISAMResponse( ReceivedCommand c )
        {
            if( c == null ) throw new ArgumentNullException( nameof( c ) );
            CommandId = c.CommandId;
            CallerId = c.CallerId;
            CorrelationId = c.CorrelationId;
        }

        /// <summary>
        /// Initializes a <see cref="VISAMCode.Synchronous"/> or <see cref="VISAMCode.Asynchronous"/> response with a
        /// result that can be null (either because the command doesn't expect any result or the expected result is
        /// actually a null reference or null nullable value type).
        /// </summary>
        /// <param name="c">The received command. Must not be null.</param>
        /// <param name="result">The result from the handler. Can be null.</param>
        public VISAMResponse( ReceivedCommand c, object result )
            : this( c )
        {
            Code = c.AsynchronousHandlingMode ? VISAMCode.Asynchronous : VISAMCode.Synchronous;
            Result = result;
        }

        /// <summary>
        /// Initializes a <see cref="VISAMCode.InternalError"/> response a result that is a <see cref="CKExceptionData"/>.
        /// </summary>
        /// <param name="c">The received command. Must not be null.</param>
        /// <param name="error">The error data. Must not be null.</param>
        public VISAMResponse( ReceivedCommand c, CKExceptionData error )
            : this( c )
        {
            if( error == null ) throw new ArgumentNullException( nameof( error ) );
            Code = VISAMCode.InternalError;
            Result = error;
        }

        /// <summary>
        /// Initializes a <see cref="VISAMCode.ValidationError"/> response.
        /// </summary>
        /// <param name="error">The error data. Must not be null.</param>
        public VISAMResponse( object validationError )
        {
            if( validationError == null ) throw new ArgumentNullException( nameof( validationError ) );
            Code = VISAMCode.ValidationError;
            Result = validationError;
        }

        /// <summary>
        /// Gets the <see cref="VISAMCode"/>.
        /// </summary>
        public VISAMCode Code { get; }

        /// <summary>
        /// Gets the error or result object (if any).
        /// Null when the command doesn't expect any result or for the initial <see cref="VISAMCode.Asynchronous"/> response.
        /// </summary>
        public object Result { get; }

        /// <summary>
        /// Gets the command identifier that has been assigned by the End Point.
        /// Note that this may be null for <see cref="VISAMCode.ValidationError"/> or an early <see cref="VISAMCode.InternalError"/>,
        /// and it is always null for <see cref="VISAMCode.Meta"/>.
        /// </summary>
        public string CommandId { get; }

        /// <summary>
        /// Gets the caller identifier.
        /// </summary>
        public string CallerId { get;}

        /// <summary>
        /// Gets the optional correlation identifier.
        /// </summary>
        public string CorrelationId { get; }

    }
}
