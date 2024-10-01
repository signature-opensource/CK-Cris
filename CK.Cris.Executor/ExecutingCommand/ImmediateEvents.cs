using CK.Core;
using CK.PerfectEvent;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Exposes a live collection of immediate <see cref="IEvent"/>: the <see cref="Added"/> event can be used
    /// to react to new events.
    /// </summary>
    public sealed class ImmediateEvents : IReadOnlyCollection<IEvent>
    {
        Node? _first;
        Node? _last;
        internal readonly PerfectEventSender<IEvent> _immediate;
        int _count;

        internal ImmediateEvents()
        {
            _immediate = new PerfectEventSender<IEvent>();
        }

        /// <summary>
        /// Raised whenever a <see cref="CrisPocoKind.RoutedImmediateEvent"/> or <see cref="CrisPocoKind.CallerOnlyImmediateEvent"/> is raised
        /// by the executing command (or recursively called commands).
        /// </summary>
        public PerfectEvent<IEvent> Added => _immediate.PerfectEvent;

        internal Task AddAndRaiseAsync( IActivityMonitor monitor, IEvent v )
        {
            var n = new Node( v );
            if( _first == null )
            {
                _first = n;
                _last = n;
            }
            else
            {
                Throw.DebugAssert( _last != null );
                _last.Next = n;
            }
            ++_count;
            return _immediate.RaiseAsync( monitor, v );
        }

        /// <summary>
        /// Non thread safe: this must NOT be called concurrently,
        /// but the collection is safe: the enumerator traverse the linked list that can
        /// only grow at the end.
        /// </summary>
        internal void Add( IEvent v )
        {
            var n = new Node( v );
            if( _first == null )
            {
                _first = n;
                _last = n;
            }
            else
            {
                Throw.DebugAssert( _last != null );
                _last.Next = n;
            }
            ++_count;
        }

        /// <inheritdoc/>
        public int Count => _count;

        sealed class Node
        {
            public readonly IEvent Value;
            public Node? Next;

            public Node( IEvent v )
            {
                Value = v;
            }
        }

        /// <summary>
        /// Implements a thread safe enumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<IEvent>
        {
            Node? _current;

            internal Enumerator( ImmediateEvents s )
            {
                _current = s._first;
            }

            /// <inheritdoc/>
            public readonly IEvent Current
            {
                get
                {
                    Throw.CheckState( _current != null );
                    return _current.Value;
                }
            }

            readonly object IEnumerator.Current => Current;

            /// <inheritdoc />
            public readonly void Dispose()
            {
            }

            /// <inheritdoc />
            public bool MoveNext()
            {
                _current = _current?.Next;
                return _current != null;
            }

            /// <summary>
            /// Not supported.
            /// </summary>
            public readonly void Reset() => Throw.NotSupportedException();
        }

        /// <inheritdoc />
        public Enumerator GetEnumerator() => new Enumerator( this );

        IEnumerator<IEvent> IEnumerable<IEvent>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
