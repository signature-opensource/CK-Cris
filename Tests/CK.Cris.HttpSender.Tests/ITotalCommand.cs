namespace CK.Cris.HttpSender.Tests
{
    public interface ITotalCommand : ICommand<ITotalResult>, ICommandWithCurrentCulture, CK.Auth.ICommandAuthNormal
    {
        string? Action { get; set; }
    }

}
