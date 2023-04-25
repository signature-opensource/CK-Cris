using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// An event is a <see cref="ICommand"/> with a special <see cref="NoWaitResult"/> result.
    /// </summary>
    public interface ICrisEvent : ICommand<ICrisEvent.NoWaitResult>
    {
        /// <summary>
        /// Type marker for result of a fire &amp; forget command: a <see cref="ICrisEvent"/>.
        /// This cannot be instantiated nor specialized.
        /// </summary>
        public sealed class NoWaitResult
        {
            NoWaitResult() { }
        }
    }

}
