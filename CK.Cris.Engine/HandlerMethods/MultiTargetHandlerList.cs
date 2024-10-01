using CK.Core;
using System.Collections;
using System.Collections.Generic;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Helper that groups the sync handlers before the async ones and maintains
    /// a <see cref="AsyncHandlerCount"/>.
    /// </summary>
    public sealed class MultiTargetHandlerList : IReadOnlyList<HandlerMultiTargetMethod>
    {
        internal static readonly MultiTargetHandlerList _empty = new MultiTargetHandlerList();

        readonly List<HandlerMultiTargetMethod> _handlers;
        int _syncCount;

        internal MultiTargetHandlerList()
        {
            _handlers = new List<HandlerMultiTargetMethod>();    
        }

        /// <summary>
        /// Gets the number of asynchronous handlers.
        /// </summary>
        public int AsyncHandlerCount => _handlers.Count - _syncCount;

        internal void Add( HandlerMultiTargetMethod m )
        {
            Throw.DebugAssert( this != _empty );
            if( m.IsRefAsync || m.IsValAsync ) _handlers.Add( m );
            else _handlers.Insert( _syncCount++, m );
        }

        /// <summary>
        /// Gets the handler at the specified index. Synchronous handlers come first followed by 
        /// <see cref="AsyncHandlerCount"/> asyncronous handlers.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The handler.</returns>
        public HandlerMultiTargetMethod this[int index] => ((IReadOnlyList<HandlerMultiTargetMethod>)_handlers)[index];

        /// <summary>
        /// Gets the total number of handlers. Synchronous handlers come first followed by 
        /// <see cref="AsyncHandlerCount"/> asyncronous handlers.
        /// </summary>
        public int Count => ((IReadOnlyCollection<HandlerMultiTargetMethod>)_handlers).Count;

        /// <inheritdoc />
        public IEnumerator<HandlerMultiTargetMethod> GetEnumerator() => ((IEnumerable<HandlerMultiTargetMethod>)_handlers).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_handlers).GetEnumerator();
    }

}
