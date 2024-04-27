using CK.Setup;
using System;
using System.Runtime.CompilerServices;

namespace CK.Cris
{
    /// <summary>
    /// Decorates a method that is a command or command part validator.
    /// The method must have at least a <see cref="CK.Core.UserMessageCollector"/> and a
    /// command (or command part) parameters.
    /// <para>
    /// This validator is executed right before the command handler, in the same Dependency Injection context
    /// and Unit of Work as the handler: such validator can be seen as a part of the prologue of the handler
    /// and could perfectly been directly called by the handler.
    /// </para>
    /// <para>
    /// The <see cref="CommandIncomingValidatorAttribute"/> defines validators that check the "surface" of a command.
    /// These validators are called in the context of the endpoints that receive the command.
    /// When the validation concerns any aspect independent of the command execution itself, it is recommended to use
    /// the CommandSyntaxValidator.
    /// </para>
    /// </summary>
    [AttributeUsage( AttributeTargets.Method, AllowMultiple = false, Inherited = false )]
    public sealed class CommandHandlingValidatorAttribute : ContextBoundDelegationAttribute
    {
        /// <summary>
        /// Initializes a new <see cref="CommandHandlingValidatorAttribute"/>.
        /// </summary>
        /// <param name="fileName">Captures the source file name of the validator definition.</param>
        /// <param name="lineNumber">Captures the source line number of the validator definition.</param>
        public CommandHandlingValidatorAttribute( [CallerFilePath] string? fileName = null, [CallerLineNumber] int lineNumber = 0 )
            : base( "CK.Setup.Cris.CommandHandlingValidatorAttributeImpl, CK.Cris.Engine" )
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
