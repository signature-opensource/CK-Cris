using CK.Core;
using CK.IO.Authentication;
using CK.IO.Authentication.Basic;
using CK.IO.Authentication.Passkey;
using CK.IO.Authentication.Weak;
using CK.Setup;
using System;

namespace CK.Testing;

/// <summary>
/// Registers the fake authentication center.
/// </summary>
public static class FakeAuthCenterTestingExtensions
{
    /// <summary>
    /// The types of the fake authentication center: <see cref="FakeActorDatabase"/>, <see cref="FakeTokenReader"/>,
    /// <see cref="FakeAuthCenterOperations"/>, and the commands and pocos that it handles.
    /// </summary>
    public static readonly Type[] FakeAuthCenterTypes =
    [
        typeof( FakeActorDatabase ),
        typeof( FakeTokenReader ),
        typeof( FakeAuthCenterOperations ),
        typeof( ISessionResult ),
        typeof( ISessionOperationResult ),
        typeof( IAuthenticationInfoPoco ),
        typeof( IActorInfoPoco ),
        typeof( IOpenSessionOperationCommand ),
        typeof( IRefreshSessionOperationCommand ),
        typeof( ILogoutSessionOperationCommand ),
        typeof( IImpersonateOperationCommand ),
        typeof( IClearImpersonationOperationCommand ),
        typeof( IGetLoginSchemesQCommand ),
        typeof( ILoginSchemeInfo ),
        typeof( IStartLoginCommand ),
        typeof( IRemoteLoginCallbackCommand ),
        typeof( IStartRemoteLoginOperationCommand ),
        typeof( ICompleteRemoteLoginOperationCommand ),
        typeof( IBasicLoginCommand ),
        typeof( IWeakLoginCommand ),
        typeof( IStartPasskeyLoginCommand ),
        typeof( IPasskeyLoginCommand ),
        typeof( IStartPasskeyRegistrationCommand ),
        typeof( IPasskeyRegistrationCommand ),
        typeof( IPasskeyRegistrationResult ),
        typeof( IStartPasskeyLoginOperationCommand ),
        typeof( IFinishPasskeyLoginOperationCommand ),
        typeof( IStartPasskeyRegistrationOperationCommand ),
        typeof( IFinishPasskeyRegistrationOperationCommand )
    ];

    /// <summary>
    /// Adds the <see cref="FakeAuthCenterTypes"/> to the BinPath: the fake authentication center that handles the
    /// operations of the session command handlers (CK.Cris.Authentication and its scheme packages, that the test
    /// registers).
    /// </summary>
    /// <param name="binPath">This BinPath configuration.</param>
    /// <returns>This BinPath configuration.</returns>
    public static BinPathConfiguration AddFakeAuthCenter( this BinPathConfiguration binPath )
    {
        binPath.Types.Add( FakeAuthCenterTypes );
        return binPath;
    }
}
