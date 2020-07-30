using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Type marker for result of a fire & forget command.
    /// </summary>
    public sealed class NoWaitResult
    {
        /// <summary>
        /// Gets a singleton instance of this marker type.
        /// </summary>
        public static NoWaitResult Instance = new NoWaitResult();

        NoWaitResult() {}
    }

}
