using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Cris
{
    /// <summary>
    /// Immutable command execution result.
    /// </summary>
    public class CommandResult
    {
        CommandResult( CommandCallStack.Frame f )
        {
            CommandId = f.CommandId;
            CallerId = f.CallerId;
            CorrelationId = f.CorrelationId;
            StartExecutionTime = f.CreateTime;
            EndExecutionTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes result that can be null (either because the command doesn't expect any result or the expected result is
        /// a null reference or value type).
        /// </summary>
        /// <param name="f">The command frame.</param>
        /// <param name="result">The result from the handler. Can be null.</param>
        public CommandResult( CommandCallStack.Frame f, object? result )
            : this( f )
        {
            Code = f.AsynchronousFrame ? VISAMCode.Asynchronous : VISAMCode.Synchronous;
            Result = result;
        }

        /// <summary>
        /// Initializes a result object that is a <see cref="CKExceptionData"/>.
        /// </summary>
        /// <param name="f">The command frame.</param>
        /// <param name="error">The error data. Must not be null.</param>
        public CommandResult( CommandCallStack.Frame f, CKExceptionData error )
            : this( f )
        {
            if( error == null ) throw new ArgumentNullException( nameof( error ) );
            Code = VISAMCode.InternalError;
            Result = error;
        }

        /// <summary>
        /// Initializes a <see cref="VISAMCode.ValidationError"/> response.
        /// </summary>
        /// <param name="error">The error data. Must not be null.</param>
        public CommandResult( object validationError )
        {
            if( validationError == null ) throw new ArgumentNullException( nameof( validationError ) );
            Code = VISAMCode.ValidationError;
            Result = validationError;
            StartExecutionTime = EndExecutionTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the <see cref="VISAMCode"/>.
        /// </summary>
        public VISAMCode Code { get; }

        /// <summary>
        /// Gets the error or result object (if any).
        /// Null when the command doesn't expect any result.
        /// </summary>
        public object? Result { get; }

        /// <summary>
        /// Gets the command identifier that has been assigned by the End Point.
        /// Note that this may be null for <see cref="VISAMCode.ValidationError"/> or an early <see cref="VISAMCode.InternalError"/>,
        /// and it is always null for <see cref="VISAMCode.Meta"/>.
        /// </summary>
        public string? CommandId { get; }

        /// <summary>
        /// Gets the caller identifier.
        /// </summary>
        public string? CallerId { get; }

        /// <summary>
        /// Gets the optional correlation identifier.
        /// </summary>
        public string? CorrelationId { get; }

        /// <summary>
        /// The start time in UTC of the command execution.
        /// </summary>
        public DateTime StartExecutionTime { get; }

        /// <summary>
        /// The end time in UTC of the command execution.
        /// </summary>
        public DateTime EndExecutionTime { get; }

        /// <summary>
        /// Gets the previous command result if a command has been executed before this one.
        /// </summary>
        public CommandResult? PreviousResult { get; }

        /// <summary>
        /// Gets the last subordinated command result if any.
        /// </summary>
        public CommandResult? LastSubordinatedResult { get; }

    }
}
