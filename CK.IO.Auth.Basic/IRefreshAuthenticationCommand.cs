using CK.Cris;

namespace CK.Auth;

/// <summary>
/// Command used to refresh the current authentication information. 
/// </summary>
public interface IRefreshAuthenticationCommand : ICommand<IAuthenticationResult>
{
    /// <summary>
    /// Gets or sets whether the authentication must be called to have a chance
    /// to re-validate the authentication.
    /// </summary>
    bool CallBackend { get; set; }
}
