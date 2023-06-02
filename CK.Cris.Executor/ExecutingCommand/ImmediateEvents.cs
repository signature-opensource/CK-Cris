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
        /// Raised whenever a <see cref="CrisPocoKind.RoutedEventImmediate"/> is raised by the executing command (or
        /// recursively called commands).
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
                Debug.Assert( _last != null );
                _last.Next = n;
            }
            ++_count;
            return _immediate.SafeRaiseAsync( monitor, v );
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
                Debug.Assert( _last != null );
                _last.Next = n;
            }
            ++_count;
        }

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

        public struct Enumerator : IEnumerator<IEvent>
        {
            Node? _current;

            internal Enumerator( ImmediateEvents s )
            {
                _current = s._first;
            }

            public IEvent Current
            {
                get
                {
                    Throw.CheckState( _current != null );
                    return _current.Value;
                }
            }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                _current = _current?.Next;
                return _current != null;
            }

            public void Reset() => Throw.NotSupportedException();
        }

        public Enumerator GetEnumerator() => new Enumerator( this );

        IEnumerator<IEvent> IEnumerable<IEvent>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
