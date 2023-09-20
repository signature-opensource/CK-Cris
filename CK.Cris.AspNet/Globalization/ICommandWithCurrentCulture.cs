using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CK.Core;

namespace CK.Cris
{
    /// <summary>
    /// Command part that specifies the <see cref="CurrentCultureInfo"/> that must be available
    /// when validating and handling the command.
    /// </summary>
    public interface ICommandWithCurrentCulture
    {
        /// <summary>
        /// Gets or sets the current culture name that must be used when processing
        /// this command. When null, the currently available <see cref="CurrentCultureInfo"/> is
        /// not changed.
        /// </summary>
        string? CurrentCultureName { get; set; }
    }
}
