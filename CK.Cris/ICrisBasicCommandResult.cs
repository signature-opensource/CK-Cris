namespace CK.Cris
{
    /// <summary>
    /// Concrete Poco for basic result of a command: whether the command succeeded and optional user messages.
    /// <para>
    /// This is extensible (like any other primary Poco) but there is  few reason to extend this basic result:
    /// its <see cref="IStandardResultPart.Success"/> and <see cref="IStandardResultPart.UserMessages"/> are
    /// enough for a "basic result".
    /// </para>
    /// </summary>
    public interface ICrisBasicCommandResult : IStandardResultPart
    {
    }
}
