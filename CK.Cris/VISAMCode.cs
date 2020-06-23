using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Defines the possible command handling response types.
    /// </summary>
    public enum VISAMCode
    {
        /// <summary>
        /// Validation error: the command failed to be validated. It has been rejected by the End Point.
        /// </summary>
        ValidationError = 'V',

        /// <summary>
        /// Internal error: an error has been raised by the handling of the command.
        /// </summary>
        InternalError = 'I',

        /// <summary>
        /// The command has successfuly been executed in a synchronous-way, its result is directly accessible by the client.
        /// </summary>
        Synchronous = 'S',

        /// <summary>
        /// The execution of the command has been deferred.
        /// </summary>
        Asynchronous = 'A',

        /// <summary>
        /// This distinguishes all meta command result.
        /// </summary>
        Meta = 'M'
    }
}
