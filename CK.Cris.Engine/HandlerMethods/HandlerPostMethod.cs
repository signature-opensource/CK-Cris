using CK.Core;
using CK.Cris;
using System.Reflection;

namespace CK.Setup.Cris;

/// <summary>
/// Captures a post handler method information.
/// </summary>
public sealed class HandlerPostMethod : HandlerBase
{
    /// <summary>
    /// Always <see cref="CrisHandlerKind.CommandPostHandler"/>.
    /// </summary>
    public override CrisHandlerKind Kind => CrisHandlerKind.CommandPostHandler;

    /// <summary>
    /// The parameter that is the command or command part.
    /// </summary>
    public readonly ParameterInfo CmdOrPartParameter;

    /// <summary>
    /// The parameter that is the command result.
    /// </summary>
    public readonly ParameterInfo? ResultParameter;

    /// <summary>
    /// Whether the result parameter type must be adapted when calling the method.
    /// </summary>
    public readonly bool MustCastResultParameter;

    internal HandlerPostMethod( CrisType crisType,
                                IStObjFinalClass owner,
                                MethodInfo method,
                                ParameterInfo[] parameters,
                                string? fileName,
                                int lineNumber,
                                ParameterInfo cmdOrPartParameter,
                                ParameterInfo? resultParameter,
                                bool mustCastResultParameter,
                                bool isRefAsync,
                                bool isValAsync )
        : base( crisType, owner, method, parameters, fileName, lineNumber, isRefAsync, isValAsync )
    {
        CmdOrPartParameter = cmdOrPartParameter;
        ResultParameter = resultParameter;
        MustCastResultParameter = mustCastResultParameter;
    }
}
