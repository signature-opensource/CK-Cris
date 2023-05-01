using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Captures the result of a <see cref="IEvent"/>, <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>
    /// validation with potential warnings.
    /// <para>
    /// This is not simply named "ValidationResult" because of the existing <see cref="System.ComponentModel.DataAnnotations.ValidationResult"/>.
    /// </para>
    /// </summary>
    public sealed class CrisValidationResult
    {
        /// <summary>
        /// The success validation result: no error, no warning.
        /// </summary>
        public static readonly CrisValidationResult SuccessResult = new CrisValidationResult( Array.Empty<Entry>(), true );

        /// <summary>
        /// A successfully completed validation task: no error, no warning.
        /// </summary>
        public static readonly Task<CrisValidationResult> SuccessResultTask = Task.FromResult( SuccessResult );

        /// <summary>
        /// A validation entry is a descriptive <see cref="Text"/> and whether this is an error or a warning. 
        /// </summary>
        public readonly struct Entry
        {
            /// <summary>
            /// The error or warning description.
            /// </summary>
            public readonly string Text;

            /// <summary>
            /// True for an error, false for a warning.
            /// </summary>
            public readonly bool IsError;

            /// <summary>
            /// Initializes a new entry.
            /// </summary>
            /// <param name="text">The error or warning description. Must not be null or empty.</param>
            /// <param name="isError">True for an error, false for a warning.</param>
            public Entry( string text, bool isError )
            {
                Throw.CheckNotNullOrEmptyArgument( text );
                Text = text;
                IsError = isError;
            }

            /// <summary>
            /// Overridden to return this <see cref="Text"/>.
            /// </summary>
            /// <returns>The text.</returns>
            public override string ToString() => Text;
        }

        CrisValidationResult( IReadOnlyList<Entry> entries, bool success )
        {
            AllEntries = entries;
            Success = success;
        }

        /// <summary>
        /// Creates a <see cref="CrisValidationResult"/> from a <see cref="ActivityMonitorExtension.CollectEntries(IActivityMonitor, out IReadOnlyList{ActivityMonitorSimpleCollector.Entry}, LogLevelFilter, int)"/>
        /// result. Only <see cref="LogLevel.Fatal"/>, <see cref="LogLevel.Error"/> and <see cref="LogLevel.Warn"/> are handled.
        /// </summary>
        /// <param name="entries">The log entries.</param>
        /// <returns>A command validator result.</returns>
        public static CrisValidationResult Create( IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries )
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
            return new CrisValidationResult( result, !hasError );
        }

        /// <summary>
        /// Initializes a new <see cref="CrisValidationResult"/> from an existing list of <see cref="Entry"/>.
        /// </summary>
        /// <param name="entries">The validation entries.</param>
        public CrisValidationResult( IReadOnlyList<Entry> entries )
                  : this( entries, entries.All( e => !e.IsError ) )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="CrisValidationResult"/> with an error.
        /// </summary>
        /// <param name="error">The validation error text.</param>
        public CrisValidationResult( string error )
                  : this( new[]{ new Entry( error, true ) }, false )
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
