namespace CK.Auth;

/// <summary>
/// What proves a session: its key and the hashes of its secrets, the refresh secret (it extends the session) and
/// the binding secret (it binds the tokens to the caller). The secrets themselves never leave the
/// endpoint that generated them: the authentication center only knows these hashes.
/// </summary>
/// <param name="SessionKey">
/// The key of the session, chosen by the authentication center (see <see cref="IAuthenticationSessionEndpoint.Issue"/>).
/// It is opaque for the endpoint and is not a secret: the endpoint keeps it with the secrets and presents it back.
/// </param>
/// <param name="RefreshSecretHash">The base64url SHA-256 of the refresh secret.</param>
/// <param name="BindingSecretHash">The base64url SHA-256 of the binding secret.</param>
public sealed record AuthenticationSessionProof( string SessionKey, string RefreshSecretHash, string BindingSecretHash );
