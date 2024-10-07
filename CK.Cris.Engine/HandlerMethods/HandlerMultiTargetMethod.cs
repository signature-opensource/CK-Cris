using CK.Core;
using CK.Cris;
using System.Reflection;

namespace CK.Setup.Cris;


/// <summary>
/// Applies to <see cref="CrisHandlerKind.IncomingValidator"/>, <see cref="CrisHandlerKind.CommandHandlingValidator"/>,
/// <see cref="CrisHandlerKind.ConfigureAmbientServices"/> and <see cref="CrisHandlerKind.RestoreAmbientServices"/>
/// (<see cref="CrisHandlerKind.RoutedEventHandler"/> use the <see cref="HandlerRoutedEventMethod"/>).
/// <para>
/// This is a void (async or not) method with a parameter that is the command, event or part and 1 or 2 specific arguments:
/// <list type="bullet">
///     <item>Command validators have a <see cref="UserMessageCollector"/>.</item>
///     <item>Ambient services configurators and restores have a <see cref="AmbientServiceHub"/>.</item>
///     <item>
///     Incoming validators have <see cref="UserMessageCollector"/> and/or a <see cref="ICrisIncomingValidationContext"/>.
///     </item>
/// </list>
/// </para>
/// Other parameters are resolved from a IServiceProvider.
/// </summary>
public sealed class HandlerMultiTargetMethod : HandlerBase
{
    readonly CrisHandlerKind _kind;

    /// <summary>
    /// The kind of handler.
    /// </summary>
    public override CrisHandlerKind Kind => _kind;

    /// <summary>
    /// The parameter that is the command, event or part.
    /// </summary>
    public readonly ParameterInfo ThisPocoParameter;

    /// <summary>
    /// The first parameter (depends on the <see cref="Kind"/>).
    /// </summary>
    public readonly ParameterInfo? ArgumentParameter;

    /// <summary>
    /// The second parameter (the <see cref="ICrisIncomingValidationContext"/> for IncomingValidator).
    /// </summary>
    public readonly ParameterInfo? ArgumentParameter2;


    internal HandlerMultiTargetMethod( CrisType crisType,
                                       CrisHandlerKind kind,
                                       IStObjFinalClass owner,
                                       MethodInfo method,
                                       ParameterInfo[] parameters,
                                       string? fileName,
                                       int lineNumber,
                                       ParameterInfo thisPocoParameter,
                                       ParameterInfo? argumentParameter,
                                       ParameterInfo? argumentParameter2,
                                       bool isRefAsync,
                                       bool isValAsync )
        : base( crisType, owner, method, parameters, fileName, lineNumber, isRefAsync, isValAsync )
    {
        Throw.DebugAssert( argumentParameter != null || argumentParameter2 != null );
        _kind = kind;
        ThisPocoParameter = thisPocoParameter;
        ArgumentParameter = argumentParameter;
        ArgumentParameter2 = argumentParameter2;
    }
}
