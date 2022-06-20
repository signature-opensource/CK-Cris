using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Simple model for errors: a list of strings.
    /// Since this is a <see cref="IPoco"/>, it can easily be extended.
    /// </summary>
    [ExternalName( "CrisSimpleError" )]
    public interface ISimpleErrorResult : IPoco
    {
        /// <summary>
        /// Gets the list of error strings.
        /// </summary>
        List<string> Errors { get; }
    }
}
