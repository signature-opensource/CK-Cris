using CK.Setup;
using System;
using System.Runtime.CompilerServices;

namespace CK.Cris
{
    /// <summary>
    /// Decorates a method that is a command (or command part) post handler.
    /// </summary>
    [AttributeUsage( AttributeTargets.Method, AllowMultiple = false, Inherited = false )]
    public sealed class CommandPostHandlerAttribute : ContextBoundDelegationAttribute
    {
        /// <summary>
        /// Initializes a new <see cref="CommandPostHandlerAttribute"/>.
        /// </summary>
        /// <param name="fileName">Captures the source file name of the handler definition.</param>
        /// <param name="lineNumber">Captures the source line number of the handler definition.</param>
        public CommandPostHandlerAttribute( [CallerFilePath] string? fileName = null, [CallerLineNumber] int lineNumber = 0 )
            : base( "CK.Setup.Cris.CommandPostHandlerAttributeImpl, CK.Cris.Engine" )
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
