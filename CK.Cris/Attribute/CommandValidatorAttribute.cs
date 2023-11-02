using CK.Setup;
using System;
using System.Runtime.CompilerServices;

namespace CK.Cris
{
    /// <summary>
    /// Decorates a method that is a command or command part validator.
    /// The method must have at least a <see cref="CK.Core.UserMessageCollector"/> and a
    /// command (or command part) parameters.
    /// </summary>
    [AttributeUsage( AttributeTargets.Method, AllowMultiple = false, Inherited = false )]
    public sealed class CommandValidatorAttribute : ContextBoundDelegationAttribute
    {
        /// <summary>
        /// Initializes a new <see cref="CommandValidatorAttribute"/>.
        /// </summary>
        /// <param name="fileName">Captures the source file name of the validator definition.</param>
        /// <param name="lineNumber">Captures the source line number of the validator definition.</param>
        public CommandValidatorAttribute( [CallerFilePath] string? fileName = null, [CallerLineNumber] int lineNumber = 0 )
            : base( "CK.Setup.Cris.CommandValidatorAttributeImpl, CK.Cris.Engine" )
        {
            FileName = fileName;
            LineNumber = lineNumber;
        }

        /// <summary>
        /// Gets the file name that defines this handler.
        /// </summary>
        public string? FileName { get; }

        /// <summary>
        /// Gets the line number that defines this handler.
        /// </summary>
        public int LineNumber { get; }

    }
}
