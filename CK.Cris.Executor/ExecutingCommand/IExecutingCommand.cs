using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// An executing command carries the <see cref="Command"/>, the <see cref="ValidationResult"/> and the eventual <see cref="RequestCompletion"/>.
    /// of a <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>.
    /// <para>
    /// This is the non generic interface of <see cref="IOutgoingCommand{T}"/>.
    /// </para>
    /// </summary>
    public interface IExecutingCommand
    {
        /// <summary>
        /// Gets the <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>.
        /// </summary>
        IAbstractCommand Command { get; }

        /// <summary>
        /// Gets a token that identifies the initialization of the command execution.
        /// </summary>
        ActivityMonitor.Token IssuerToken { get; }

        /// <summary>
        /// Gets the UTC date and time creation of this executing command.
        /// </summary>
        DateTime CreationDate { get; }

        /// <summary>
        /// Gets the validation result of the executing command.
        /// When this <see cref="CrisValidationResult.Success"/> is false,
        /// the <see cref="Completion"/> contains a <see cref="ICrisResultError"/> with the <see cref="CrisValidationResult.Messages"/>
        /// lines. 
        /// </summary>
        Task<CrisValidationResult> ValidationResult { get; }

        /// <summary>
        /// Gets a task that is completed when the execution is terminated.
        /// The task's value can be:
        /// <list type="bullet">
        ///  <item>A <see cref="ICrisResultError"/> on validation or execution error.</item>
        ///  <item>A successful null result on success when <see cref="Command"/> is a <see cref="ICommand"/>.</item>
        ///  <item>A successful result object when this command is a <see cref="ICommand{TResult}"/>.</item>
        /// </list>
        /// </summary>
        Task<object?> Completion { get; }

        /// <summary>
        /// Gets a live collection of events emitted by the command execution.
        /// <para>
        /// This is a thread safe collection that is updated during the execution.
        /// The <see cref="ImmediateEvents.Added"/> event is raised only for <see cref="CrisPocoKind.RoutedImmediateEvent"/> event.
        /// (see <see cref="ICrisPoco.CrisPocoModel"/>.<see cref="ICrisPocoModel.Kind">Kind</see>).
        /// </para>
        /// </summary>
        ImmediateEvents ImmediateEvents { get; }

        /// <summary>
        /// Gets the non immediate events emitted by the command.
        /// This is non empty only when the command has been successfully executed.
        /// </summary>
        IReadOnlyList<IEvent> Events { get; }
    }
}
