using CK.Core;
using CK.IO.Authentication;
using CK.IO.Authentication.Basic;
using CK.IO.Authentication.Passkey;
using CK.IO.Authentication.Weak;
using CK.Setup;
using System;

namespace CK.Testing;

/// <summary>
/// Registers the fake authentication center, or the fake login providers of a real one.
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
    /// The login providers of a real authentication center, on the <see cref="FakeActorDatabase"/>:
    /// <see cref="FakeBasicLoginProvider"/>, <see cref="FakeWeakLoginProvider"/>, <see cref="FakeOidcLoginProvider"/> and its
    /// <see cref="FakeAutoBindingAccountService"/>.
    /// The passkey provider (<see cref="FakePasskeyLoginProvider"/> and its <see cref="InMemoryPasskeyStore"/>) is added
    /// with the passkey scheme family.
    /// </summary>
    public static readonly Type[] FakeLoginProviderTypes =
    [
        typeof( FakeActorDatabase ),
        typeof( FakeBasicLoginProvider ),
        typeof( FakeWeakLoginProvider ),
        typeof( FakeOidcLoginProvider ),
        typeof( FakeAutoBindingAccountService )
    ];

    /// <summary>
    /// Adds the <see cref="FakeLoginProviderTypes"/> to the BinPath: the actors and the login providers of a real
    /// authentication center (CK.AppIdentity.Cris.AuthCenter.Local) that the test runs without a database.
    /// </summary>
    /// <param name="binPath">This BinPath configuration.</param>
    /// <param name="withPasskeys">True to also add the <see cref="FakePasskeyLoginProvider"/> and its <see cref="InMemoryPasskeyStore"/>.</param>
    /// <returns>This BinPath configuration.</returns>
    public static BinPathConfiguration AddFakeLoginProviders( this BinPathConfiguration binPath, bool withPasskeys = false )
    {
        binPath.Types.Add( FakeLoginProviderTypes );
        if( withPasskeys ) binPath.Types.Add( typeof( InMemoryPasskeyStore ), typeof( FakePasskeyLoginProvider ) );
        return binPath;
    }

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
