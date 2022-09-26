using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Cris
{
    /// <summary>
    /// Describes the final result of a command.
    /// The result's type of a command is not constrained (the TResult in <see cref="ICommand{TResult}"/> can be anything),
    /// this represents the final result of the handling of a command with its <see cref="VESACode"/> and any errors or correlation
    /// information.
    /// </summary>
    [ExternalName( "CrisResult" )]
    public interface ICrisResult : IPoco
    {
        /// <summary>
        /// Gets or sets the <see cref="VESACode"/>.
        /// </summary>
        VESACode Code { get; set; }

        /// <summary>
        /// Gets or sets the error or result object (if any).
        /// <list type="bullet">
        ///   <item>
        ///     This is null when the command has been processed synchronously, successfully, and doesn't expect any result
        ///     (the command is not a <see cref="ICommand{TResult}"/>).
        ///   </item>
        ///   <item>
        ///     This is also always null if the command is a <see cref="ICommand{TResult}"/> where TResult is <see cref="NoWaitResult"/>.
        ///   </item>
        ///   <item>
        ///     If the <see cref="Code"/> is <see cref="VESACode.Asynchronous"/> this may contain a command identifier or other correlation
        ///     identifier that may be used to bind to/recover/track the asynchronous result (if the <see cref="CorrelationId"/> is not enough
        ///     and some extra data is required).
        ///   </item>
        ///   <item>
        ///     On error (<see cref="VESACode.Error"/> or <see cref="VESACode.ValidationError"/>), this should contain a description of
        ///     the error, typically a <see cref="ICrisResultError"/>, a simple string, a value tuple, or any combination of
        ///     types that are easily serializable.
        ///   </item>
        /// </list>
        /// </summary>
        object? Result { get; set; }

        /// <summary>
        /// Gets or sets an optional identifier that identifies the handling of the command
        /// regardless of this <see cref="Code"/>.
        /// <para>
        /// This identifier must of course be unique.
        /// </para>
        /// </summary>
        string? CorrelationId { get; set; }

    }
}
