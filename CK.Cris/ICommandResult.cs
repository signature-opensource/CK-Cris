using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Cris
{
    /// <summary>
    /// Describes the result of a command.
    /// </summary>
    [ExternalName( "CrisResult" )]
    public interface ICommandResult : IPoco
    {
        /// <summary>
        /// Gets or sets the <see cref="VESACode"/>.
        /// </summary>
        VESACode Code { get; set; }

        /// <summary>
        /// Gets or sets the error or result object (if any).
        /// Null when the command doesn't expect any result or if the <see cref="Code"/> is <see cref="VESACode.Asynchronous"/>.
        /// On error (<see cref="VESACode.Error"/> or <see cref="VESACode.ValidationError"/>), this should contain a description of
        /// the error, typically a <see cref="ISimpleErrorResult"/>, a simple string, a value tuple, or any combination of
        /// types that serializable (ie. registered).
        /// </summary>
        object? Result { get; set; }

    }
}
