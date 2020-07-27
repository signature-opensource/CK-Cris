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
        /// Gets or sets the <see cref="VESACode"/>.
        /// </summary>
        VESACode Code { get; set; }

        /// <summary>
        /// Gets or sets the error or result object (if any).
        /// Null when the command doesn't expect any result or if the <see cref="Code"/> is <see cref="VESACode.Asynchronous"/>.
        /// On error, this should contain a description of the error that can be modelled as a poco, a simple string, a value tuple,
        /// or any combination of types that are serializable.
        /// </summary>
        object? Result { get; set; }

    }
}
