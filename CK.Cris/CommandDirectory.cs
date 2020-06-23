using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CK.Cris
{
    /// <summary>
    /// Command directory that contains all the available command in the context.
    /// </summary>
    [CK.Setup.ContextBoundDelegation( "CK.Setup.Cris.CommandDirectoryImpl, CK.Cris.Engine" )]
    public abstract class CommandDirectory : ISingletonAutoService
    {
        readonly IReadOnlyDictionary<object, ICommandModel> _index;

        protected CommandDirectory( (IReadOnlyList<ICommandModel> commands, IReadOnlyDictionary<object,ICommandModel> index) data )
        {
            Commands = data.commands;
            _index = data.index;
        }

        /// <summary>
        /// Tries to find a command model of an actual command object.
        /// </summary>
        /// <param name="command">The command for which a model must be found.</param>
        /// <returns>The model or null if not found.</returns>
        public ICommandModel? FindModel( ICommand command ) => command != null ? _index.GetValueOrDefault( command.GetType() ) : null;

        /// <summary>
        /// Tries to find a command model from one of its name (may be a <see cref="ICommandModel.PreviousNames"/>).
        /// </summary>
        /// <param name="commandName">The command name.</param>
        /// <returns>The model or null if not found.</returns>
        public ICommandModel? FindModel( string commandName ) => _index.GetValueOrDefault( commandName );

        /// <summary>
        /// Finds a <see cref="KnownCommand"/>. Throws a <see cref="ArgumentException"/> if not found.
        /// </summary>
        /// <param name="command">The command for which a model must be found.</param>
        /// <returns>The command and its associated <see cref="ICommandModel"/>.</returns>
        public KnownCommand Find( ICommand command )
        {
            if( command == null ) throw new ArgumentNullException( nameof( command ) );
            var model = FindModel( command );
            if( model == null ) throw new ArgumentException( "Unknown Command type.", nameof( command ) );
            return new KnownCommand( model, command );
        }

        /// <summary>
        /// Gets all the commands indexed by their <see cref="ICommandModel.CommandIdx"/>.
        /// </summary>
        public IReadOnlyList<ICommandModel> Commands { get; }

    }
}
