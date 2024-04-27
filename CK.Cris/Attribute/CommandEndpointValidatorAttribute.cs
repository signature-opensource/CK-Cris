using CK.Setup;
using System;
using System.Runtime.CompilerServices;

namespace CK.Cris
{
    /// <summary>
    /// Decorates a method that is a command or command part endpoint validator.
    /// The validator is called in the context of the endpoint that receive the command: it can use any ambient
    /// or processwide singletons services (typically related to authentication, tenancy, culture, etc.).
    /// <para>
    /// Using "execution" services that are close to the execution of the command, should be avoided:
    /// to validate a command in the same Unit of Work as its execution, use <see cref="CommandValidatorAttribute"/> instead.
    /// </para>
    /// </summary>
    [AttributeUsage( AttributeTargets.Method, AllowMultiple = false, Inherited = false )]
    public sealed class CommandEndpointValidatorAttribute : ContextBoundDelegationAttribute
    {
        /// <summary>
        /// Initializes a new <see cref="CommandEndpointValidatorAttribute"/>.
        /// </summary>
        /// <param name="fileName">Captures the source file name of the validator definition.</param>
        /// <param name="lineNumber">Captures the source line number of the validator definition.</param>
        public CommandEndpointValidatorAttribute( [CallerFilePath] string? fileName = null, [CallerLineNumber] int lineNumber = 0 )
            : base( "CK.Setup.Cris.CommandEndpointValidatorAttributeImpl, CK.Cris.Engine" )
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
