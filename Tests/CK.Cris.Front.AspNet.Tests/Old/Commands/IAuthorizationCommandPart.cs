namespace CK.Cris.Tests
{
    public interface IAuthorizationCommandPart : ICommandPart
    {
        int ActorId { get; set; }
    }
}
