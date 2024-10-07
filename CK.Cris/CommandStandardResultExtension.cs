using CK.Core;

namespace CK.Cris;

/// <summary>
/// Extends <see cref="IStandardResultPart"/>.
/// </summary>
public static class CommandStandardResultExtension
{
    /// <summary>
    /// Fills <see cref="IStandardResultPart.UserMessages"/> from a <see cref="UserMessageCollector"/>.
    /// By default, <see cref="IStandardResultPart.Success"/> is set to false if a <see cref="UserMessageLevel.Error"/>
    /// exists in the messages.
    /// <para>
    /// This doesn't clear any current UserMessages: messages from the <paramref name="collector"/> are appended.
    /// Similarly, if Success is already false, it remains false even if no Error message appears in the collector.
    /// </para>
    /// </summary>
    /// <param name="result">This result part to configure.</param>
    /// <param name="collector">The user message collector.</param>
    /// <param name="updateSuccess">False to not update Success flag.</param>
    /// <returns>The <see cref="IStandardResultPart.Success"/> value.</returns>
    public static bool SetUserMessages( this IStandardResultPart result, UserMessageCollector collector, bool updateSuccess = true )
    {
        Throw.CheckNotNullArgument( result );
        Throw.CheckNotNullArgument( collector );
        bool success = result.Success;
        foreach( var userMessage in collector.UserMessages )
        {
            if( userMessage.IsValid )
            {
                success &= userMessage.Level != UserMessageLevel.Error;
                result.UserMessages.Add( userMessage );
            }
        }
        if( updateSuccess ) result.Success = success;
        return result.Success;
    }

}
