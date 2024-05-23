namespace CK.Cris.HttpSender.Tests
{
    public interface ITotalResult : ICommandStandardResult
    {
        int ActorId { get; set; }
        string CultureName { get; set; }
    }

}
