using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using static CK.Cris.CommandCallStack;

namespace CK.Cris
{
    /// <summary>
    /// A simple class stack that is a stack (actually a list) of <see cref="Frame"/>.
    /// </summary>
    public sealed class CommandCallStack : IReadOnlyList<Frame>
    {
        readonly List<Frame> _frames;

        /// <summary>
        /// Basic information related to a call.
        /// </summary>
        public class Frame
        {
            internal Frame( string commandId, string callerId, string correlationId, CommandResult? previousResult )
            {
                CommandId = commandId;
                CallerId = callerId;
                CorrelationId = correlationId;
                CreateTime = DateTime.UtcNow;
                PreviousResult = previousResult;
            }

            /// <summary>
            /// The command identifier that is assigned by the End Point.
            /// </summary>
            public string CommandId { get; }

            /// <summary>
            /// The caller identifier. 
            /// </summary>
            public string CallerId { get; }

            /// <summary>
            /// The optional correlation identifier.
            /// </summary>
            public string? CorrelationId { get; }

            /// <summary>
            /// The creation time in UTC of this frame.
            /// </summary>
            public DateTime CreateTime { get; }

            /// <summary>
            /// Gets whether this is an asynchrounous frame: the result is not
            /// awaited and will be received later.
            /// </summary>
            public bool AsynchronousFrame { get; }

            /// <summary>
            /// Gets any the result of the previously executed command if any.
            /// </summary>
            public CommandResult? PreviousResult { get; }

            /// <summary>
            /// Overridden to display all fields.
            /// </summary>
            /// <returns>A readable string.</returns>
            public override string ToString() => $"Id: {CommandId}, CallerId: {CallerId}, CorrelationId: {CorrelationId}, CreateTime: {CreateTime}";
        }

        public CommandCallStack()
        {
            _frames = new List<Frame>();
        }

        /// <summary>
        /// Gets the frame by its index: 0 is the root, initial call.
        /// </summary>
        /// <param name="index">The frame index.</param>
        /// <returns>The Frame.</returns>
        public Frame this[int index] => _frames[index];

        /// <summary>
        /// Gets the current number of frames.
        /// </summary>
        public int Count => _frames.Count;

        /// <summary>
        /// Supports iterating on these <see cref="Frame"/>s.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<Frame> GetEnumerator() => _frames.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _frames.GetEnumerator();


    }
}
