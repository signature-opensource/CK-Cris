extern alias Legacy;

using CK.Core;
using CK.Testing;
using NUnit.Framework;
using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.Authentication.Conflict.Tests;

/// <summary>
/// CK.Cris.Auth (legacy, CK.Auth.Abstractions) and CK.Cris.Authentication (CK.AuthenticationInfo) define the same
/// CK.Auth.CrisAuthenticationService type: as soon as both assemblies are referenced, the generated code cannot
/// be compiled, whatever the registered service is. This project references both on purpose (the legacy one through
/// the "Legacy" alias). Each service alone is tested by its own test project.
/// </summary>
[TestFixture]
public class ConflictTests
{
    // Everything the legacy service needs, so that a failure can only come from the conflict.
    public sealed class LegacyUserInfoProvider : global::CK.Auth.IUserInfoProvider
    {
        public ValueTask<global::CK.Auth.IUserInfo> GetUserInfoAsync( IActivityMonitor monitor, int userId )
            => ValueTask.FromResult<global::CK.Auth.IUserInfo>( new global::CK.Auth.StdUserInfo( 0, null ) );
    }

    public sealed class UserInfoProvider : IUserInfoProvider
    {
        public ValueTask<UserInfo?> GetUserInfoAsync( IActivityMonitor monitor, int userId ) => ValueTask.FromResult<UserInfo?>( null );
    }

    static async Task ShouldFailWithConflictAsync( params Type[] types )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( types );
        configuration.FirstBinPath.Types.Add( typeof( CrisDirectory ) );
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            var r = await configuration.RunAsync();
            r.Status.ShouldBe( CK.Setup.RunStatus.Failed );
            texts.ShouldContain( t => t.Contains( "error CS0433" )
                                      && t.Contains( "'CrisAuthenticationService' exists in both 'CK.Cris.Auth" )
                                      && t.Contains( "'CK.Cris.Authentication" ) );
        }
    }

    [Test]
    public async Task the_new_service_cannot_be_used_when_the_legacy_package_is_installed_Async()
    {
        await ShouldFailWithConflictAsync( typeof( global::CK.Auth.CrisAuthenticationService ),
                                           typeof( AuthenticationInfo ),
                                           typeof( AuthenticationInfoDefaultProvider ),
                                           typeof( UserInfoProvider ) );
    }

    [Test]
    public async Task the_legacy_service_cannot_be_used_when_the_new_package_is_installed_Async()
    {
        await ShouldFailWithConflictAsync( typeof( Legacy::CK.Auth.CrisAuthenticationService ),
                                           typeof( global::CK.Auth.IAuthenticationInfo ),
                                           typeof( global::CK.Auth.StdAuthenticationTypeSystem ),
                                           typeof( LegacyUserInfoProvider ) );
    }

    [Test]
    public async Task both_services_cannot_be_installed_together_Async()
    {
        await ShouldFailWithConflictAsync( typeof( Legacy::CK.Auth.CrisAuthenticationService ),
                                           typeof( global::CK.Auth.IAuthenticationInfo ),
                                           typeof( global::CK.Auth.StdAuthenticationTypeSystem ),
                                           typeof( LegacyUserInfoProvider ),
                                           typeof( global::CK.Auth.CrisAuthenticationService ),
                                           typeof( AuthenticationInfo ),
                                           typeof( AuthenticationInfoDefaultProvider ),
                                           typeof( UserInfoProvider ) );
    }
}
