using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Cris
{
    /// <summary>
    /// Captures the result of a command validation with potential warnings.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Initializes a new <see cref="ValidationResult"/>.
        /// </summary>
        /// <param name="entries">The logged entries.</param>
        /// <param name="command">The command.</param>
        /// <param name="success">Whether the validation suceeded.</param>
        public ValidationResult( IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries, ICommand command, bool success )
        {
            AllEntries = entries;
            Command = command;
            Success = success;
        }

        /// <summary>
        /// Initializes a new <see cref="ValidationResult"/>.
        /// </summary>
        /// <param name="entries">The logged entries.</param>
        /// <param name="command">The command.</param>
        public ValidationResult( IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries, ICommand command )
                  : this( entries, command, entries.All( e => e.MaskedLevel < LogLevel.Error ) )
        {
        }

        /// <summary>
        /// Initializes a new successful <see cref="ValidationResult"/> (no warning, no error).
        /// <param name="command">The command.</param>
        /// </summary>
        public ValidationResult( ICommand command )
            : this( Array.Empty<ActivityMonitorSimpleCollector.Entry>(), command, true )
        {
        }

        /// <summary>
        /// Gets the command.
        /// </summary>
        public ICommand Command { get; }

        /// <summary>
        /// Gets whether the command has been successfuly validated. <see cref="Errors"/> is empty
        /// but there may be <see cref="Warnings"/>.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets whether there are <see cref="Warnings"/> messages.
        /// </summary>
        public bool HasWarnings => AllEntries.Any( e => e.MaskedLevel == LogLevel.Warn );

        /// <summary>
        /// Gets the errors.
        /// </summary>
        public IEnumerable<string> Errors => AllEntries.Where( e => e.MaskedLevel >= LogLevel.Error ).Select( e => e.Text );

        /// <summary>
        /// Gets the warnings.
        /// </summary>
        public IEnumerable<string> Warnings => AllEntries.Where( e => e.MaskedLevel == LogLevel.Warn ).Select( e => e.Text );

        /// <summary>
        /// Gets all the available entries, regardless of their <see cref="ActivityMonitorSimpleCollector.Entry.MaskedLevel"/>.
        /// </summary>
        public IReadOnlyList<ActivityMonitorSimpleCollector.Entry> AllEntries { get; }


    }
}
