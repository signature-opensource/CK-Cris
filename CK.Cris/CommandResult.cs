using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Cris
{
    /// <summary>
    /// Immutable command execution result.
    /// </summary>
    public class CommandResult : ICommandResult
    {
        CommandResult( VISAMCode code, object? result, CommandCallerInfo? caller, DateTime startExecutionTime, DateTime? endExecutionTime )
        {
            Code = code;
            Result = result;
            Caller = caller;
            StartExecutionTime = startExecutionTime;
            EndExecutionTime = endExecutionTime;
        }

        /// <summary>
        /// Initializes a <see cref="VISAMCode.ValidationError"/> response.
        /// The <see cref="Code"/> is <see cref="VISAMCode.ValidationError"/>.
        /// </summary>
        /// <param name="startValidationTime">The datetime (must be UTC) at which the validation started.</param>
        /// <param name="error">The error data. Must not be null.</param>
        /// <param name="caller">Optional caller information.</param>
        public static CommandResult ValidationError( DateTime startValidationTime, object validationError, CommandCallerInfo? caller = null )
        {
            if( validationError == null ) throw new ArgumentNullException( nameof( validationError ) );
            return new CommandResult( VISAMCode.ValidationError, validationError, caller, startValidationTime, DateTime.UtcNow );
        }

        /// <summary>
        /// Initializes an error command result: the result is a <see cref="CKExceptionData"/>.
        /// The <see cref="Code"/> is <see cref="VISAMCode.InternalError"/>.
        /// </summary>
        /// <param name="startExecutionTime">The datetime (must be UTC) at which the execution started.</param>
        /// <param name="error">The error data. Must not be null.</param>
        /// <param name="caller">Optional caller information.</param>
        public static CommandResult InternalError( DateTime startExecutionTime, CKExceptionData error, CommandCallerInfo? caller = null )
        {
            if( error == null ) throw new ArgumentNullException( nameof( error ) );
            return new CommandResult( VISAMCode.InternalError, error, caller, startExecutionTime, DateTime.UtcNow );
        }

        /// <summary>
        /// Initializes an error command result: the result is an error message.
        /// The <see cref="Code"/> is <see cref="VISAMCode.InternalError"/>.
        /// </summary>
        /// <param name="startExecutionTime">The datetime (must be UTC) at which the execution started.</param>
        /// <param name="errorMessage">The error message. Must not be <see cref="String.IsNullOrWhiteSpace(string)"/>.</param>
        /// <param name="caller">Optional caller information.</param>
        public static CommandResult InternalError( DateTime startExecutionTime, string errorMessage, CommandCallerInfo? caller = null )
        {
            if( String.IsNullOrWhiteSpace( errorMessage ) ) throw new ArgumentNullException( nameof( errorMessage ) );
            return new CommandResult( VISAMCode.InternalError, errorMessage, caller, startExecutionTime, DateTime.UtcNow );
        }

        /// <summary>
        /// Initializes a successful command execution outcome.
        /// The <paramref name="result"/> can be null (either because the command doesn't expect any result or the expected result is
        /// a null reference or value type).
        /// The <see cref="Code"/> is <see cref="VISAMCode.Synchronous"/>.
        /// </summary>
        /// <param name="startExecutionTime">The datetime (must be UTC) at which the execution started.</param>
        /// <param name="result">The result from the handler. Can be null.</param>
        /// <param name="caller">Optional caller information.</param>
        public static CommandResult SynchronousResult( DateTime startExecutionTime, object? result, CommandCallerInfo? caller = null )
        {
            return new CommandResult( VISAMCode.Synchronous, result, caller, startExecutionTime, DateTime.UtcNow );
        }

        /// <summary>
        /// Initializes an asynchrous command execution start: the caller information is required and its
        /// <see cref="CommandCallerInfo.CommandId"/> must be set (ie. not null) since the eventual result will
        /// have to be sent to the <see cref="CommandCallerInfo.CallerId"/> later.
        /// The <see cref="Code"/> is <see cref="VISAMCode.Asynchronous"/>.
        /// </summary>
        /// <param name="startExecutionTime">The datetime (must be UTC) at which the command has been handled.</param>
        /// <param name="caller">The required caller information.</param>
        public static CommandResult AsynchronousExecution( DateTime startExecutionTime, CommandCallerInfo caller )
        {
            if( caller == null ) throw new ArgumentNullException( nameof( caller ) );
            if( caller.CommandId == null ) throw new ArgumentException( "A command identifier must be assigned.", nameof( caller ) );
            return new CommandResult( VISAMCode.Asynchronous, null, caller, startExecutionTime, null );
        }

        /// <summary>
        /// Initializes a meta information result.
        /// The <see cref="Code"/> is <see cref="VISAMCode.Meta"/>.
        /// </summary>
        /// <param name="startExecutionTime">The datetime (must be UTC) at which the meta request has been handled.</param>
        /// <param name="caller">Optional caller information.</param>
        public static CommandResult MetaInformation( DateTime startExecutionTime, object meta, CommandCallerInfo? caller = null )
        {
            if( meta == null ) throw new ArgumentNullException( nameof( meta ) );
            return new CommandResult( VISAMCode.Meta, meta, caller, startExecutionTime, DateTime.UtcNow );
        }


        /// <summary>
        /// Gets the <see cref="VISAMCode"/>.
        /// </summary>
        public VISAMCode Code { get; }

        /// <summary>
        /// Gets the error or result object (if any).
        /// Null when the command doesn't expect any result or if the <see cref="Code"/> is <see cref="VISAMCode.Asynchronous"/>.
        /// On error, this is either a string or a <see cref="CKExceptionData"/>.
        /// </summary>
        public object? Result { get; }

        /// <summary>
        /// The start time in UTC of the command execution.
        /// </summary>
        public DateTime StartExecutionTime { get; }

        /// <summary>
        /// The end time in UTC of the command execution.
        /// This is null when <see cref="Code"/> is <see cref="VISAMCode.Asynchronous"/>.
        /// </summary>
        public DateTime? EndExecutionTime { get; }

        /// <summary>
        /// Gets the <see cref="CommandCallerInfo"/> if one has been specified.
        /// </summary>
        public CommandCallerInfo? Caller { get; }

        /// <summary>
        /// Sets the <see cref="Caller"/> information if it is currently null (this throws a <see cref="InvalidOperationException"/>
        /// if it's already set to another caller information).
        /// </summary>
        /// <param name="caller">The caller information to set.</param>
        /// <returns>This command result or an updated one.</returns>
        public CommandResult InitializeCaller( CommandCallerInfo? caller )
        {
            if( Caller == caller ) return this;
            if( Caller != null ) throw new InvalidOperationException( "Command result has already a different Caller defined." );
            return new CommandResult( Code, Result, caller, StartExecutionTime, EndExecutionTime );
        }
    }
}
