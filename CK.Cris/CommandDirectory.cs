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
        protected CommandDirectory( IReadOnlyList<ICommandModel> commands )
        {
            Commands = commands;
        }

        /// <summary>
        /// Gets all the commands indexed by their <see cref="ICommandModel.CommandIdx"/>.
        /// </summary>
        public IReadOnlyList<ICommandModel> Commands { get; }

    }
}
