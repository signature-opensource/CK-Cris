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
        public async Task ICommandAuthUnsafe_cannot_be_fooled_on_its_ActorId_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IUnsafeCommand ), typeof( UnsafeHandler ), typeof( CrisExecutionContext ) );
            using( var s = new CrisTestServer( c, withAuthentication: true ) )
            {
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""YES"",""ActorId"":0}]" );
                    Debug.Assert( r != null );
                    var result = await s.GetCrisResultWithNullCorrelationIdAsync( r );
                    result.ToString().Should().Be( @"{""code"":83,""result"":null,""correlationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""YES. There is no ActorId in the Json => it is 0 by default.""}]" );
                    Debug.Assert( r != null );
                    var result = await s.GetCrisResultWithNullCorrelationIdAsync( r );
                    result.ToString().Should().Be( @"{""code"":83,""result"":null,""correlationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""NO WAY!"",""ActorId"":3712}]" );
                    Debug.Assert( r != null );
                    var result = await s.GetCrisResultWithNullCorrelationIdAsync( r );
                    result.ToString().Should().Be( @"{""code"":86,""result"":[""CrisResultError"",{""errors"":[""Invalid actor identifier: the command provided identifier doesn\u0027t match the current authentication.""]}],""correlationId"":null}" );
                }
                UnsafeHandler.LastUserInfo = null;
                await s.LoginAsync( "Albert" );
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""userInfo"":""Yes! Albert 3712 is logged in."",""actorId"":3712}]" );
                    Debug.Assert( r != null );
                    var result = await s.GetCrisResultWithNullCorrelationIdAsync( r );
                    result.ToString().Should().Be( @"{""code"":83,""result"":null,""correlationId"":null}" );
                    UnsafeHandler.LastUserInfo.Should().Be( "Yes! Albert 3712 is logged in." );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""NO WAY!"",""ActorId"":7}]" );
                    Debug.Assert( r != null );
                    var result = await s.GetCrisResultWithNullCorrelationIdAsync( r );
                    result.ToString().Should().Be( @"{""code"":86,""result"":[""CrisResultError"",{""errors"":[""Invalid actor identifier: the command provided identifier doesn\u0027t match the current authentication.""]}],""correlationId"":null}" );
                }
                await s.LogoutAsync();
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""NO! Albert is no more here."",""ActorId"":3712}]" );
                    Debug.Assert( r != null );
                    var result = await s.GetCrisResultWithNullCorrelationIdAsync( r );
                    result.ToString().Should().Be( @"{""code"":86,""result"":[""CrisResultError"",{""errors"":[""Invalid actor identifier: the command provided identifier doesn\u0027t match the current authentication.""]}],""correlationId"":null}" );
                }
            }
        }

    }
}
