using CK.Core;
using System;

namespace CK.Auth;

/// <summary>
/// The session side of the endpoint that received a session command (<c>CK.IO.Authentication.ICommandAuthenticationSession</c>).
/// Each endpoint container implements it: the HTTP endpoint with its cookies, an AppIdentity endpoint with a session
/// bound to the remote party. There is no implementation in the background: a session command is executed by its
/// receiving endpoint only.
/// <para>
/// The endpoint generates the session secrets and keeps them: only their hashes are exposed (<see cref="AuthenticationSessionProof"/>).
/// </para>
/// </summary>
[ScopedContainerConfiguredService]
public interface IAuthenticationSessionEndpoint : IAutoService
{
    /// <summary>
    /// Gets the proof of the session presented by the caller (the session key and the hashes of the secrets it presents).
    /// Null when the caller presents no session.
    /// </summary>
    AuthenticationSessionProof? PresentedProof { get; }

    /// <summary>
    /// Generates new secrets for a new or a rotated session. The endpoint keeps them and sends them
    /// back to the caller at the end of the request (replacing any previous ones).
    /// </summary>
    /// <param name="sessionKey">
    /// The key of the session, chosen by the authentication center: the endpoint keeps it with the secrets and presents
    /// it back (see <see cref="AuthenticationSessionProof.SessionKey"/>).
    /// </param>
    /// <param name="rememberMe">Whether the session must survive the end of the caller session (a browser session for instance).</param>
    /// <param name="refreshExpires">The expiration of the refresh secret (Utc): the end of the session.</param>
    /// <returns>The session key and the hashes of the new secrets.</returns>
    AuthenticationSessionProof Issue( string sessionKey, bool rememberMe, DateTime refreshExpires );

    /// <summary>
    /// Clears the session secrets of the caller (logout).
    /// </summary>
    void Clear();
}
