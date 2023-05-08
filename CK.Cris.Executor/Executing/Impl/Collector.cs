using CK.Core;
using CK.PerfectEvent;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Cris
{

    sealed class Collector<TEmitter, T> : ICollector<TEmitter, T> where T : class
    {
        Node? _first;
        Node? _last;
        readonly PerfectEventSender<TEmitter, T> _added;
        IBridge? _eventBridge;
        int _count;

        internal Collector( PerfectEventSender<TEmitter, T>? onEventRelay )
        {
            _added = new PerfectEventSender<TEmitter, T>();
            if( onEventRelay != null )
            {
                _eventBridge = _added.CreateRelay( onEventRelay );
            }
        }

        public void Close() => _eventBridge?.Dispose();

        /// <summary>
        /// Non thread safe: this must NOT be called concurrently,
        /// but the collection is safe: the enumerator traverse the linked list that can
        /// only grow at the end.
        /// </summary>
        internal Task AddAsync( IActivityMonitor monitor, TEmitter c, T v )
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
            return _added.SafeRaiseAsync( monitor, c, v );
        }

        public PerfectEvent<TEmitter, T> Added => _added.PerfectEvent;

        public int Count => _count;

        sealed class Node
        {
            public readonly T Value;
            public Node? Next;

            public Node( T v )
            {
                Value = v;
            }
        }

        public struct Enumerator : IEnumerator<T>
        {
            Node? _current;

            internal Enumerator( Collector<TEmitter, T> s )
            {
                _current = s._first;
            }

            public T Current
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

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
