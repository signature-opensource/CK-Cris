using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Captures the result of a <see cref="IEvent"/>, <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>
    /// validation with potential warnings.
    /// <para>
    /// This is a simple immutable capture of a <see cref="UserMessageCollector"/>.
    /// </para>
    /// <para>
    /// This is not simply named "ValidationResult" because of the existing <see cref="System.ComponentModel.DataAnnotations.ValidationResult"/>.
    /// </para>
    /// </summary>
    public sealed class CrisValidationResult
    {
        /// <summary>
        /// The success validation result: no error, no warning, no information.
        /// </summary>
        public static readonly CrisValidationResult SuccessResult = new CrisValidationResult();

        /// <summary>
        /// A successfully completed validation task: no error, no warning, no information.
        /// </summary>
        public static readonly Task<CrisValidationResult> SuccessResultTask = Task.FromResult( SuccessResult );

        CrisValidationResult()
        {
            Messages = Array.Empty<UserMessage>();
            Success = true;
        }

        /// <summary>
        /// Initializes a new validation result from a <see cref="UserMessageCollector"/>.
        /// </summary>
        /// <param name="collector">The message collector.</param>
        /// <param name="logKey">Optional <see cref="ActivityMonitor.LogKey"/> that enables to locate the logs of the validation.</param>
        public CrisValidationResult( UserMessageCollector collector, string? logKey )
        {
            Messages = collector.UserMessages.ToArray();
            Success = collector.ErrorCount == 0;
            LogKey = logKey;
        }

        /// <summary>
        /// Initializes a new validation result for an unhandled exception.
        /// </summary>
        /// <param name="messages">The messages.</param>
        /// <param name="logKey">Required log key.</param>
        public CrisValidationResult( List<UserMessage> messages, string logKey )
        {
            Throw.CheckNotNullOrEmptyArgument( messages );
            Throw.CheckNotNullOrEmptyArgument( logKey );
            Messages = messages.ToArray();
            LogKey = logKey;
        }

        /// <summary>
        /// Direct constructor from a non empty array of messages.
        /// The array must not be mutated, this is inteded to be used by external de/serializer.
        /// </summary>
        /// <param name="messages">The messages. Must not be empty: use <see cref="Success"/> singleton in such case.</param>
        /// <param name="logKey">Optional log key.</param>
        public CrisValidationResult( UserMessage[] messages, string? logKey )
        {
            Throw.CheckNotNullOrEmptyArgument( messages );
            Success = messages.All( m => m.Level != UserMessageLevel.Error );
            Messages = messages;
            LogKey = logKey;
        }

        /// <summary>
        /// Gets all the messages.
        /// </summary>
        public IReadOnlyList<UserMessage> Messages { get; }

        /// <summary>
        /// Gets whether the command has been successfully validated: <see cref="Messages"/> is empty
        /// or has no <see cref="UserMessageLevel.Error"/>.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets whether there are <see cref="UserMessageLevel.Warn"/> messages.
        /// </summary>
        public bool HasWarnings => Messages.Any( e => e.Level == UserMessageLevel.Warn );

        /// <summary>
        /// <see cref="ActivityMonitor.LogKey"/> that enables to locate the logs of the validation.
        /// It may not always be available.
        /// </summary>
        public string? LogKey { get; }

    }
}
