using CK.Core;
using System.Collections.Generic;

namespace CK.Cris.AspNet
{
    /// <summary>
    /// Describes the final result of a command.
    /// <para>
    /// The result's type of a command is not constrained (the TResult in <see cref="ICommand{TResult}"/> can be anything) or
    /// a <see cref="IAspNetCrisResultError"/>.
    /// </para>
    /// <para>
    /// This is for "API adaptation" of ASPNet endpoint that has no available back channel and can be called by agnostic
    /// process.
    /// </para>
    /// </summary>
    [ExternalName( "AspNetResult" )]
    public interface IAspNetCrisResult : IPoco
    {
        /// <summary>
        /// Gets or sets the error or result object (if any).
        /// <list type="bullet">
        ///   <item>
        ///     A <see cref="IAspNetCrisResultError"/> on error.
        ///   </item>
        ///   <item>
        ///     null for a successful a <see cref="ICommand"/>.
        ///   </item>
        ///   <item>
        ///     The result of a <see cref="ICommand{TResult}"/>.
        ///   </item>
        /// </list>
        /// </summary>
        object? Result { get; set; }

        /// <summary>
        /// Gets an optional list of <see cref="UserMessageLevel.Info"/>, <see cref="UserMessageLevel.Warn"/> or <see cref="UserMessageLevel.Error"/>
        /// messages issued by the validation of the command.
        /// Validation error messages also appear in the <see cref="IAspNetCrisResultError.Errors"/>.
        /// </summary>
        List<SimpleUserMessage>? ValidationMessages { get; set; }

        /// <summary>
        /// Gets or sets an optional correlation identifier.
        /// </summary>
        string? CorrelationId { get; set; }

    }
}
