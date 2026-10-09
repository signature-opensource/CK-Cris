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
    /// Gets the bearer token presented by the caller. Null when the caller presents none.
    /// <para>
    /// A relay sends it to a remote authentication center, that reads it again (it never trusts the front).
    /// </para>
    /// </summary>
    string? PresentedToken { get; }

    /// <summary>
    /// Generates the secrets that the next <see cref="Issue"/> will use and returns their hashes. A relay calls it
    /// before sending a session command to a remote authentication center: the hashes travel with the command, the
    /// secrets stay here. When <see cref="Issue"/> is called after this, it uses these secrets (and returns these hashes).
    /// </summary>
    /// <returns>The hashes of the prepared secrets.</returns>
    (string RefreshSecretHash, string BindingSecretHash) PrepareSecrets();

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

    /// <summary>
    /// Gets the URL to which a remote provider (OpenID Connect, OAuth...) redirects the caller at the end of a remote
    /// login: the callback of this endpoint, that builds the <c>CK.IO.Authentication.IRemoteLoginCallbackCommand</c>.
    /// Null when this endpoint cannot receive remote logins (an AppIdentity channel for instance).
    /// <para>
    /// The authentication center accepts only the callback URLs configured for the remote scheme.
    /// </para>
    /// </summary>
    string? RemoteLoginCallbackUrl { get; }

    /// <summary>
    /// Generates the correlation secret of a remote login that starts and returns its hash: the authentication center
    /// keeps the hash with the pending login, the secret stays with the caller (a cookie for an HTTP endpoint), so that
    /// only the caller that started the login can complete it (login CSRF).
    /// Called only when <see cref="RemoteLoginCallbackUrl"/> is not null.
    /// </summary>
    /// <returns>The hash of the correlation secret.</returns>
    string PrepareRemoteLogin();

    /// <summary>
    /// Gets the hash of the correlation secret presented by the caller when it completes a remote login.
    /// Null when the caller presents none.
    /// </summary>
    string? PresentedRemoteLoginHash { get; }

    /// <summary>
    /// Gets the origin (<c>scheme://host[:port]</c>) of the web page that sent the command, as checked by this endpoint
    /// (an HTTP endpoint accepts same-origin requests only). Null when this endpoint doesn't serve web pages (an
    /// AppIdentity channel for instance).
    /// <para>
    /// The browser signs the origin in the passkey (WebAuthn) responses: the authentication center accepts a passkey
    /// ceremony only from an endpoint with an origin that it is configured to accept, and checks that the signed origin is
    /// this one. This value only selects: the authentication center's configuration is the control.
    /// </para>
    /// </summary>
    string? WebOrigin { get; }
}
