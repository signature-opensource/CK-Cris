using CK.Core;
using System.Collections.Generic;

namespace CK.Cris;

/// <summary>
/// Describes the final result of a command.
/// <para>
/// The result's type of a command is not constrained (the TResult in <see cref="ICommand{TResult}"/> can be anything) or
/// a <see cref="ICrisResultError"/>.
/// </para>
/// <para>
/// This is mainly for "API adaptation" endpoints that has no available back channel and can be called by agnostic
/// process or (TypeScript front).
/// </para>
/// </summary>
[ExternalName( "CrisCallResult" )]
public interface ICrisCallResult : IPoco
{
    /// <summary>
    /// Gets or sets the error or result object (if any).
    /// <list type="bullet">
    ///   <item>
    ///     A <see cref="ICrisResultError"/> on error.
    ///   </item>
    ///   <item>
    ///     null for a successful a <see cref="ICommand"/>.
    ///   </item>
    ///   <item>
    ///     The result of a <see cref="ICommand{TResult}"/> (that can be null).
    ///   </item>
    /// </list>
    /// </summary>
    object? Result { get; set; }

    /// <summary>
    /// Gets an optional list of <see cref="UserMessageLevel.Info"/>, <see cref="UserMessageLevel.Warn"/> or <see cref="UserMessageLevel.Error"/>
    /// messages issued by the validation of the command.
    /// Validation error messages also appear in the <see cref="ICrisResultError.Errors"/>.
    /// </summary>
    List<UserMessage>? ValidationMessages { get; set; }

    /// <summary>
    /// Gets or sets an optional correlation identifier.
    /// </summary>
    string? CorrelationId { get; set; }
}
