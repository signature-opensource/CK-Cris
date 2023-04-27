using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Captures the result of a command validation with potential warnings.
    /// <para>
    /// This is not simply named "ValidationResult" because of the existing <see cref="System.ComponentModel.DataAnnotations.ValidationResult"/>.
    /// </para>
    /// </summary>
    public sealed class CommandValidationResult
    {
        /// <summary>
        /// The success validation result: no error, no warning.
        /// </summary>
        public static readonly CommandValidationResult SuccessResult = new CommandValidationResult( Array.Empty<ActivityMonitorSimpleCollector.Entry>(), true );

        /// <summary>
        /// A successfully completed validation task: no error, no warning.
        /// </summary>
        public static readonly Task<CommandValidationResult> SuccessResultTask = Task.FromResult( SuccessResult );

        CommandValidationResult( IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries, bool success )
        {
            AllEntries = entries;
            Success = success;
        }

        /// <summary>
        /// Initializes a new <see cref="CommandValidationResult"/>.
        /// </summary>
        /// <param name="entries">The logged entries.</param>
        public CommandValidationResult( IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries )
                  : this( entries, entries.All( e => e.MaskedLevel < LogLevel.Error ) )
        {
        }

        /// <summary>
        /// Gets whether the command has been successfully validated. <see cref="Errors"/> is empty
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
