using CK.Core;
using System.Collections.Generic;
using System.ComponentModel;

namespace CK.Cris
{
    /// <summary>
    /// Defines a standard result part.
    /// <para>
    /// Use <see cref="CommandStandardResultExtension.SetUserMessages(ICommandStandardResult, UserMessageCollector, bool)"/> extension
    /// method to easily configure this part from a <see cref="UserMessageCollector"/>.
    /// </para>
    /// </summary>
    [CKTypeDefiner]
    public interface ICommandStandardResult : IPoco
    {
        /// <summary>
        /// Gets or sets whether the command succeeded or failed.
        /// Deaults to true.
        /// </summary>
        [DefaultValue( true )]
        bool Success { get; set; }

        /// <summary>
        /// Gets a mutable list of user messages.
        /// It is easier to use <see cref="UserMessageCollector"/> and the
        /// extension method <see cref="CommandStandardResultExtension.SetUserMessages(ICommandStandardResult, UserMessageCollector, bool)"/>.
        /// </summary>
        List<UserMessage> UserMessages { get; }
    }
}
