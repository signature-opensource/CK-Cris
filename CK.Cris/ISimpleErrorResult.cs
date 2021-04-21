using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Simple model for errors: a list of strings.
    /// </summary>
    [ExternalName( "CrisSimpleError" )]
    public interface ISimpleErrorResult : IPoco
    {
        /// <summary>
        /// Gets the list of error strings.
        /// </summary>
        IList<string> Errors { get; }
    }
}
