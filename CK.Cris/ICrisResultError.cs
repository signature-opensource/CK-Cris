using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Simple model for errors: a list of <see cref="UserMessage"/>.
    /// Since this is a <see cref="IPoco"/>, it can easily be extended.
    /// <para>
    /// You can use the helper <see cref="PocoFactoryExtensions.Create(IPocoFactory{ICrisResultError}, UserMessage, UserMessage[])"/> extension
    /// method to create a error from user messages.
    /// </para>
    /// </summary>
    [ExternalName( "CrisResultError" )]
    public interface ICrisResultError : IPoco
    {
        /// <summary>
        /// Gets or sets whether the command failed during validation or execution.
        /// </summary>
        bool IsValidationError { get; set; }

        /// <summary>
        /// Gets the list of user messages.
        /// At least one of them should be a <see cref="UserMessageLevel.Error"/> but this is not checked.
        /// </summary>
        List<UserMessage> UserMessages { get; }

        /// <summary>
        /// Gets or sets a <see cref="ActivityMonitor.LogKey"/> that enables to locate the logs of the command execution.
        /// It may not always be available.
        /// </summary>
        public string? LogKey { get; set; }
    }
}
