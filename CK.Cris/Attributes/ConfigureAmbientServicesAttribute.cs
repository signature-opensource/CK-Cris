using CK.Core;
using CK.Setup;
using System;
using System.Runtime.CompilerServices;

namespace CK.Cris
{
    /// <summary>
    /// Decorates a method that can configure <see cref="AmbientServiceHub"/> from a command or a command part.
    /// The method is called in the context of the endpoint that receive the command once the [IncomingValidator]
    /// have validated the command.
    /// </summary>
    [AttributeUsage( AttributeTargets.Method, AllowMultiple = false, Inherited = false )]
    public sealed class ConfigureAmbientServicesAttribute : ContextBoundDelegationAttribute
    {
        /// <summary>
        /// Initializes a new <see cref="ConfigureAmbientServicesAttribute"/>.
        /// </summary>
        /// <param name="fileName">Captures the source file name of the validator definition.</param>
        /// <param name="lineNumber">Captures the source line number of the validator definition.</param>
        public ConfigureAmbientServicesAttribute( [CallerFilePath] string? fileName = null, [CallerLineNumber] int lineNumber = 0 )
            : base( "CK.Setup.Cris.ConfigureAmbientServicesAttributeImpl, CK.Cris.Engine" )
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
