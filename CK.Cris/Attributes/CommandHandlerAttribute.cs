using CK.Setup;
using System;
using System.Runtime.CompilerServices;

namespace CK.Cris;

/// <summary>
/// Decorates a method that is a <see cref="IAbstractCommand"/> handler.
/// </summary>
[AttributeUsage( AttributeTargets.Method, AllowMultiple = false, Inherited = false )]
public sealed class CommandHandlerAttribute : ContextBoundDelegationAttribute
{
    /// <summary>
    /// Initializes a new <see cref="CommandHandlerAttribute"/>.
    /// </summary>
    /// <param name="fileName">Captures the source file name of the handler definition.</param>
    /// <param name="lineNumber">Captures the source line number of the handler definition.</param>
    public CommandHandlerAttribute( [CallerFilePath] string? fileName = null, [CallerLineNumber] int lineNumber = 0 )
        : base( "CK.Setup.Cris.CommandHandlerAttributeImpl, CK.Cris.Engine" )
    {
        FileName = fileName;
        LineNumber = lineNumber;
    }

    /// <summary>
    /// Gets or sets whether the <see cref="IAbstractCommand"/> that the method accepts doesn't
    /// need to be a unified interface of all the interfaces that define the <see cref="IAbstractCommand"/>.
    /// Defaults to false: the "closed interface requirement" is the rule but if the handler is able
    /// to adapt its behavior to the actual type of the command, this can be set to true.
    /// <para>
    /// An example of such handlers are the [SqlProcedure] implementation (CK.DB.SqlServer packages) that generates
    /// code based on the <see cref="ICrisPoco.CrisPocoModel"/> of the type.
    /// </para>
    /// </summary>
    public bool AllowUnclosedCommand { get; set; }

    /// <summary>
    /// Gets the file name that defines this handler.
    /// </summary>
    public string? FileName { get; }

    /// <summary>
    /// Gets the line number that defines this handler.
    /// </summary>
    public int LineNumber { get; }
}
