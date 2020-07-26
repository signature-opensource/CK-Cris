using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Cris
{
    /// <summary>
    /// Describes the result of a command.
    /// </summary>
    public interface ICommandResult : IPoco
    {
        /// <summary>
        /// Gets or sets the start time of the command handling.
        /// </summary>
        DateTime StartExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="VISAMCode"/>.
        /// </summary>
        VISAMCode Code { get; set; }

        /// <summary>
        /// Gets or set the end time of the execution.
        /// </summary>
        DateTime? EndExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the error or result object (if any).
        /// Null when the command doesn't expect any result or if the <see cref="Code"/> is <see cref="VISAMCode.Asynchronous"/>.
        /// On error, this should be either a string, a <see cref="IList{string}"/>, an <see cref="Exception"/> or a <see cref="CKExceptionData"/>.
        /// </summary>
        object? Result { get; set; }

        /// <summary>
        /// Gets an optional list of warnings.
        /// </summary>
        IList<string> Warnings { get; }

        /// <summary>
        /// Gets an optional list of warnings.
        /// </summary>
        IList<string> Infos { get; }

    }
}
