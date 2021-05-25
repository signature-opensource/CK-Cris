using CK.Auth;
using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.AspNet.Tests.AuthTests
{
    [TestFixture]
    public class AuthenticatedCommandTests
    {

        [ExternalName( "UnsafeCommand" )]
        public interface IUnsafeCommand : ICommandAuthUnsafe
        {
            string UserInfo { get; set; }
        }

        public class UnsafeHandler : IAutoService
        {
            [CommandHandler]
            public void Execute( IUnsafeCommand cmd )
            {
                cmd.UserInfo.Should().NotStartWith( "NO" );
                LastUserInfo = cmd.UserInfo;
            }

            static public string? LastUserInfo;
        }

        [Test]
        public async Task ICommandAuthUnsafe_cannot_be_fooled_on_its_ActorId()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IUnsafeCommand ), typeof( UnsafeHandler ) );
            using( var s = new CrisTestServer( c, true ) )
            {
                {
                    HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""YES"",""ActorId"":0}]" );
                    Debug.Assert( r != null );
                    r.StatusCode.Should().Be( HttpStatusCode.OK );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Be( @"[""CrisResult"",{""Code"":83,""Result"":null}]" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""YES. There is no ActorId in the Json => it is 0 by default.""}]" );
                    Debug.Assert( r != null );
                    r.StatusCode.Should().Be( HttpStatusCode.OK );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Be( @"[""CrisResult"",{""Code"":83,""Result"":null}]" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""NO WAY!"",""ActorId"":3712}]" );
                    Debug.Assert( r != null );
                    r.StatusCode.Should().Be( HttpStatusCode.BadRequest );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Be( @"[""CrisResult"",{""Code"":86,""Result"":[""CrisSimpleError"",{""Errors"":[""Invalid actor identifier: the command provided identifier doesn\u0027t match the current authentication.""]}]}]" );
                }
                UnsafeHandler.LastUserInfo = null;
                await s.LoginAsync( "Albert" );
                {
                    HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""Yes! Albert 3712 is logged in."",""ActorId"":3712}]" );
                    Debug.Assert( r != null );
                    r.StatusCode.Should().Be( HttpStatusCode.OK );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Be( @"[""CrisResult"",{""Code"":83,""Result"":null}]" );
                    UnsafeHandler.LastUserInfo.Should().Be( "Yes! Albert 3712 is logged in." );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""NO WAY!"",""ActorId"":7}]" );
                    Debug.Assert( r != null );
                    r.StatusCode.Should().Be( HttpStatusCode.BadRequest );
                }
                await s.LogoutAsync();
                {
                    HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""NO! Albert is no more here."",""ActorId"":3712}]" );
                    Debug.Assert( r != null );
                    r.StatusCode.Should().Be( HttpStatusCode.BadRequest );
                }
            }
        }

    }
}
