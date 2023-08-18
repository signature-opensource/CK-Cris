using CK.Core;
using CK.Cris;
using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using static CK.Core.CheckedWriteStream;

namespace CK.Cris
{
    /// <summary>
    /// Non generic base class for <see cref="ExecutingCommand{T}"/>.
    /// An executing command carries the <see cref="Command"/>, the <see cref="ValidationResult"/> and the eventual <see cref="Completion"/>.
    /// of a <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>.
    /// </summary>
    public class ExecutingCommand : IExecutingCommand, IDarkSideExecutingCommand
    {
        readonly IAbstractCommand _command;
        readonly ActivityMonitor.Token _issuerToken;
        readonly TaskCompletionSource<CrisValidationResult> _validation;
        readonly TaskCompletionSource<object?> _completion;
        readonly internal ImmediateEvents _immediate;
        IReadOnlyList<IEvent> _events;

        internal ExecutingCommand( IAbstractCommand command,
                                   ActivityMonitor.Token issuerToken )
        {
            _issuerToken = issuerToken;
            _command = command;
            _validation = new TaskCompletionSource<CrisValidationResult>();
            _completion = new TaskCompletionSource<object?>();
            _immediate = new ImmediateEvents();
            _events = Array.Empty<IEvent>();
        }

        /// <inheritdoc />
        public IAbstractCommand Command => _command;

        /// <inheritdoc />
        public ActivityMonitor.Token IssuerToken => _issuerToken;

        /// <inheritdoc />
        public DateTime CreationDate => _issuerToken.CreationDate.TimeUtc;

        /// <summary>
        /// Gets the validation result of the executing command.
        /// When this <see cref="CrisValidationResult.Success"/> is false,
        /// the <see cref="Completion"/> contains a <see cref="ICrisResultError"/> with the <see cref="CrisValidationResult.Errors"/>
        /// lines. 
        /// </summary>
        public Task<CrisValidationResult> ValidationResult => _validation.Task;

        /// <summary>
        /// Gets a task that is completed when the execution is terminated.
        /// The task's value can be:
        /// <list type="bullet">
        ///  <item>A <see cref="ICrisResultError"/> on validation or execution error.</item>
        ///  <item>A successful null result on success when this command is a <see cref="ICommand"/>.</item>
        ///  <item>A successful result object if this command is a <see cref="ICommand{TResult}"/>.</item>
        /// </list>
        /// </summary>
        public Task<object?> Completion => _completion.Task;

        /// <summary>
        /// Gets a live collection of events emitted by the command execution.
        /// <para>
        /// This is a thread safe collection that is updated during the execution.
        /// The <see cref="ImmediateEvents.Added"/> event is raised only for <see cref="CrisPocoKind.RoutedImmediateEvent"/> event.
        /// (see <see cref="ICrisPoco.CrisPocoModel"/>.<see cref="ICrisPocoModel.Kind">Kind</see>).
        /// </para>
        /// </summary>
        public ImmediateEvents ImmediateEvents => _immediate;

        /// <inheritdoc />
        public IReadOnlyList<IEvent> Events => _events;

        /// <summary>
        /// Gets the dark side of this executing command.
        /// </summary>
        public IDarkSideExecutingCommand DarkSide => this;

        void IDarkSideExecutingCommand.SetValidationResult( CrisValidationResult v, ICrisResultError? validationError )
        {
            _validation.SetResult( v );
            if( validationError != null )
            {
                DarkSide.SetResult( Array.Empty<IEvent>(), validationError );
            }
        }

        Task IDarkSideExecutingCommand.AddImmediateEventAsync( IActivityMonitor monitor, IEvent e )
        {
            return _immediate.AddAndRaiseAsync( monitor, e );
        }

        void IDarkSideExecutingCommand.SetResult( IReadOnlyList<IEvent> events, object? result )
        {
            _events = events;
            _completion.SetResult( result );
        }

        void IDarkSideExecutingCommand.SetException( Exception ex )
        {
            _completion.SetException( ex );
        }
    }

}

