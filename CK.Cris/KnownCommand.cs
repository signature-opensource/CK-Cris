using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Cris
{
    /// <summary>
    /// Captures a command and its resolved <see cref="ICommandModel"/>.
    /// </summary>
    public readonly struct KnownCommand
    {
        /// <summary>
        /// Gets the command Poco.
        /// </summary>
        public ICommand Command { get; }

        /// <summary>
        /// Get the command model.
        /// This <see cref="Command"/> object is necessarily assignable
        /// to this <see cref="ICommandModel.CommandType"/>.
        /// </summary>
        public ICommandModel Model { get; }

        /// <summary>
        /// Initializes a known command.
        /// </summary>
        /// <param name="c">The command object.</param>
        /// <param name="m">The command's model.</param>
        public KnownCommand( ICommand c, ICommandModel m )
        {
            if( c == null ) throw new ArgumentNullException( nameof( c ) );
            if( m == null ) throw new ArgumentNullException( nameof( m ) );
            if( !m.CommandType.IsAssignableFrom( c.GetType() ) ) throw new ArgumentException();
            Command = c;
            Model = m;
        }

        // Internal unchecked.
        internal KnownCommand( ICommandModel m, ICommand c )
        {
            Command = c;
            Model = m;
        }
    }
}
