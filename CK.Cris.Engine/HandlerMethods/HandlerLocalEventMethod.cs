using CK.Core;
using CK.Cris;
using System.Reflection;

namespace CK.Setup.Cris;

/// <summary>
/// Captures a local event handler method information.
/// </summary>
public sealed class HandlerLocalEventMethod : HandlerBase
{
    /// <summary>
    /// Always <see cref="CrisHandlerKind.LocalEventHandler"/>.
    /// </summary>
    public override CrisHandlerKind Kind => CrisHandlerKind.LocalEventHandler;

    /// <summary>
    /// The parameter that is the event or event part.
    /// </summary>
    public readonly ParameterInfo EventOrPartParameter;

    internal HandlerLocalEventMethod( CrisType crisType,
                                       IStObjFinalClass owner,
                                       MethodInfo method,
                                       ParameterInfo[] parameters,
                                       string? fileName,
                                       int lineNumber,
                                       ParameterInfo eventOrPartParameter,
                                       bool isRefAsync,
                                       bool isValAsync )
        : base( crisType, owner, method, parameters, fileName, lineNumber, isRefAsync, isValAsync )
    {
        EventOrPartParameter = eventOrPartParameter;
    }
}
