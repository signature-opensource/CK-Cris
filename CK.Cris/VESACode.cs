using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Defines the possible command handling response types.
    /// </summary>
    [ExternalName( "VESACode" )]
    public enum VESACode
    {
        /// <summary>
        /// Validation error: the command failed to be validated. It has been rejected by the End Point.
        /// </summary>
        ValidationError = 'V',

        /// <summary>
        /// An error has been raised by the handling of the command.
        /// </summary>
        Error = 'E',

        /// <summary>
        /// The command has successfuly been executed in a synchronous-way, its result is directly accessible by the client.
        /// </summary>
        Synchronous = 'S',

        /// <summary>
        /// The execution of the command has been deferred.
        /// </summary>
        Asynchronous = 'A'
    }
}
