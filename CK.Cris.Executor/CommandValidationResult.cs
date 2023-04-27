using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public static readonly CommandValidationResult SuccessResult = new CommandValidationResult( Array.Empty<Entry>(), true );

        /// <summary>
        /// A successfully completed validation task: no error, no warning.
        /// </summary>
        public static readonly Task<CommandValidationResult> SuccessResultTask = Task.FromResult( SuccessResult );

        /// <summary>
        /// A validation entry is a descriptive <see cref="Text"/> and whether this is an error or a warning. 
        /// </summary>
        /// <param name="Text">The error or warning.</param>
        /// <param name="IsError">True for an error, false for a warning.</param>
        public readonly record struct Entry( string Text, bool IsError );

        CommandValidationResult( IReadOnlyList<Entry> entries, bool success )
        {
            AllEntries = entries;
            Success = success;
        }

        /// <summary>
        /// Creates a <see cref="CommandValidationResult"/> from a <see cref="ActivityMonitorExtension.CollectEntries(IActivityMonitor, out IReadOnlyList{ActivityMonitorSimpleCollector.Entry}, LogLevelFilter, int)"/>
        /// result. Only <see cref="LogLevel.Fatal"/>, <see cref="LogLevel.Error"/> and <see cref="LogLevel.Warn"/> are handled.
        /// </summary>
        /// <param name="entries">The log entries.</param>
        /// <returns>A command validator result.</returns>
        public static CommandValidationResult Create( IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries )
        {
            if( entries.Count == 0 ) return SuccessResult;
            var result = new Entry[entries.Count];
            bool hasError = false;
            for( int i = 0; i < entries.Count; ++i )
            {
                var e = entries[i];
                var isError = e.MaskedLevel >= LogLevel.Error;
                hasError |= isError;
                result[i] = new Entry( e.Text, isError );
            }
            return new CommandValidationResult( result, !hasError );
        }

        /// <summary>
        /// Initializes a new <see cref="CommandValidationResult"/> from an existing list of <see cref="Entry"/>.
        /// </summary>
        /// <param name="entries">The validation entries.</param>
        public CommandValidationResult( IReadOnlyList<Entry> entries )
                  : this( entries, entries.All( e => !e.IsError ) )
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
        public bool HasWarnings => AllEntries.Any( e => !e.IsError );

        /// <summary>
        /// Gets the errors.
        /// </summary>
        public IEnumerable<string> Errors => AllEntries.Where( e => e.IsError ).Select( e => e.Text );

        /// <summary>
        /// Gets the warnings.
        /// </summary>
        public IEnumerable<string> Warnings => AllEntries.Where( e => !e.IsError ).Select( e => e.Text );

        /// <summary>
        /// Gets all the errors and warnings.
        /// </summary>
        public IReadOnlyList<Entry> AllEntries { get; }

    }
}
