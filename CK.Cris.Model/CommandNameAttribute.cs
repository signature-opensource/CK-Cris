using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Cris
{
    /// <summary>
    /// Decorates a <see cref="ICommand"/> interface with its name and optional previous names.
    /// Without this attribute, the command name is the <see cref="Type.FullName"/> of the interface
    /// that closes the the <see cref="IClosedPoco"/> command.
    /// </summary>
    [AttributeUsage( AttributeTargets.Interface, AllowMultiple = false, Inherited = false )]
    public class CommandNameAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="CommandNameAttribute"/> with the current name and a set of optional previous names.
        /// </summary>
        /// <param name="name">The current name.</param>
        /// <param name="previousNames">Any number of previous names.</param>
        public CommandNameAttribute( string name, params string[] previousNames )
        {
            if( String.IsNullOrWhiteSpace( name ) )
            {
                throw new ArgumentNullException( nameof( name ) );
            }
            if( previousNames.Any( n => String.IsNullOrWhiteSpace( n ) ) )
            {
                throw new ArgumentException( "Empty name is invalid.", nameof( previousNames ) );
            }
            if( previousNames.Contains( name ) || previousNames.GroupBy( Util.FuncIdentity ).Count() > 1 )
            {
                throw new ArgumentException( "Duplicate names in attribute.", nameof( previousNames ) );
            }
            CommandName = name;
            PreviousNames = previousNames;
        }

        /// <summary>
        /// Gets the command name.
        /// </summary>
        public string CommandName { get; }

        /// <summary>
        /// Gets the previous names.
        /// </summary>
        public IReadOnlyCollection<string> PreviousNames { get; }
    }
}
