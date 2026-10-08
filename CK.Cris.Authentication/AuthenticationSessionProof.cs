namespace CK.Auth;

/// <summary>
/// The hashes of the secrets that prove a session: the refresh secret (it extends the session) and
/// the binding secret (it binds the tokens to the caller). The secrets themselves never leave the
/// endpoint that generated them: the authentication center only knows these hashes.
/// </summary>
/// <param name="RefreshSecretHash">The base64url SHA-256 of the refresh secret.</param>
/// <param name="BindingSecretHash">The base64url SHA-256 of the binding secret.</param>
public sealed record AuthenticationSessionProof( string RefreshSecretHash, string BindingSecretHash );
