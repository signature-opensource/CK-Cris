using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// An executing command carries the <see cref="Command"/>, the observable <see cref="ImmediateEvents"/> and the
    /// eventual <see cref="ExecutedCommand"/> of a <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>.
    /// <para>
    /// This is the non generic interface of <see cref="IExecutingCommand{T}"/>.
    /// </para>
    /// </summary>
    public interface IExecutingCommand : IDeferredCommandExecutionContext
    {
        /// <summary>
        /// Gets the <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>.
        /// </summary>
        IAbstractCommand Command { get; }

        /// <summary>
        /// Gets a task that is completed when the execution is terminated with the <see cref="IExecutedCommand"/>
        /// </summary>
        Task<IExecutedCommand> ExecutedCommand { get; }

        /// <summary>
        /// Gets a live collection of events emitted by the command execution.
        /// This is a thread safe collection that is updated during the execution: the <see cref="ImmediateEvents.Added"/> event
        /// can be used to observe new events.
        /// <para>
        /// Only <see cref="CrisPocoKind.RoutedImmediateEvent"/> or <see cref="CrisPocoKind.CallerOnlyImmediateEvent"/> events appear
        /// here (see <see cref="ICrisPoco.CrisPocoModel"/>.<see cref="ICrisPocoModel.Kind">Kind</see>).
        /// </para>
        /// </summary>
        ImmediateEvents ImmediateEvents { get; }
    }
}
