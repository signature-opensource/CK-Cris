using CK.Core;
using System;

namespace CK.Cris
{
    /// <summary>
    /// The abstract base command interface marker is a simple <see cref="IPoco"/>.
    /// <para>
    /// This super definer is not intended to be used directly: <see cref="ICrisEvent"/>,
    /// <see cref="ICommand"/> and <see cref="ICommand{TResult}"/> are the interfaces to use
    /// to define events, commands without result and commands with a result.
    /// </para>
    /// </summary>
    [CKTypeSuperDefiner]
    public interface IAbstractCommand : IPoco
    {
        /// <summary>
        /// Gets the <see cref="ICommandModel"/> that describes this command.
        /// This property is automatically implemented. 
        /// </summary>
        [AutoImplementationClaim]
        ICommandModel CommandModel { get; }
    }

}
