using CK.Core;
using System.Collections.Generic;

namespace CK.Cris.AspNet
{
    /// <summary>
    /// Simplified <see cref="ICrisResultError"/>.
    /// </summary>
    [ExternalName( "AspNetCrisResultError" )]
    public interface IAspNetCrisResultError : IPoco
    {
        /// <summary>
        /// Gets or sets whether the command failed during validation or execution.
        /// </summary>
        bool IsValidationError { get; set; }

        /// <summary>
        /// Gets one or more error messages.
        /// </summary>
        IList<string> Errors { get; }

        /// <summary>
        /// Gets or sets a <see cref="ActivityMonitor.LogKey"/> that enables to locate the logs of the command execution.
        /// It may not always be available.
        /// </summary>
        string? LogKey { get; set; }
    }
}
