using CK.Core;
using System.Collections.Generic;
using System.ComponentModel;

namespace CK.Cris;

/// <summary>
/// Defines a standard result part with a <see cref="Success"/> flag and a list of <see cref="UserMessage"/>.
/// <para>
/// Use the <see cref="SetUserMessages(UserMessageCollector, bool)"/> method to easily configure this part from a <see cref="UserMessageCollector"/>.
/// </para>
/// </summary>
[CKTypeDefiner]
public interface IStandardResultPart : IPoco
{
    /// <summary>
    /// Gets or sets whether the command succeeded or failed.
    /// Defaults to true.
    /// </summary>
    [DefaultValue( true )]
    bool Success { get; set; }

    /// <summary>
    /// Gets a mutable list of user messages.
    /// It is easier to use <see cref="UserMessageCollector"/> and <see cref="SetUserMessages(UserMessageCollector, bool)"/>.
    /// </summary>
    IList<UserMessage> UserMessages { get; }

    /// <summary>
    /// Fills <see cref="UserMessages"/> from a <see cref="UserMessageCollector"/>.
    /// By default, <see cref="Success"/> is set to false if a <see cref="UserMessageLevel.Error"/>
    /// exists in the messages.
    /// <para>
    /// This doesn't clear any current UserMessages: messages from the <paramref name="collector"/> are appended.
    /// Similarly, if Success is already false, it remains false even if no Error message appears in the collector.
    /// </para>
    /// </summary>
    /// <param name="collector">The user message collector.</param>
    /// <param name="updateSuccess">False to not update Success flag.</param>
    /// <returns>The <see cref="Success"/> value.</returns>
    public bool SetUserMessages( UserMessageCollector collector, bool updateSuccess = true )
    {
        Throw.CheckNotNullArgument( collector );
        bool success = Success;
        foreach( var userMessage in collector.UserMessages )
        {
            if( userMessage.IsValid )
            {
                success &= userMessage.Level != UserMessageLevel.Error;
                UserMessages.Add( userMessage );
            }
        }
        if( updateSuccess ) Success = success;
        return Success;
    }

}
