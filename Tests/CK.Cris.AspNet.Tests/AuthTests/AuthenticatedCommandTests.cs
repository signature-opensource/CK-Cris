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
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Be( @"{""code"":83,""result"":null,""correlationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""YES. There is no ActorId in the Json => it is 0 by default.""}]" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Be( @"{""code"":83,""result"":null,""correlationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""NO WAY!"",""ActorId"":3712}]" );
                    Throw.DebugAssert( r != null );
                    ICrisResult result = await s.GetCrisResultAsync( r );
                    var correlationId = ActivityMonitor.Token.Parse( result.CorrelationId );
                    var error = result.Result as ISimpleCrisResultError;
                    Debug.Assert( error != null );
                    error.IsValidationError.Should().BeTrue();
                    error.Messages.Should().HaveCount( 1 );
                    var errorMessage = error.Messages[0];
                    errorMessage.Message.Should().Be( "Invalid actor identifier: the command provided identifier doesn't match the current authentication." );
                    errorMessage.Depth.Should().Be( 0 );
                    errorMessage.Level.Should().Be( UserMessageLevel.Error );
                    var errorLogKey = ActivityMonitor.LogKey.Parse( error.LogKey );
                    error.ToString().Should().Match( """{"isValidationError":true,"messages":[[16,0,"Invalid actor identifier: the command provided identifier doesn*t match the current authentication."]],"logKey":"*"}""" );
                }
                UnsafeHandler.LastUserInfo = null;
                await s.LoginAsync( "Albert" );
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""userInfo"":""Yes! Albert 3712 is logged in."",""actorId"":3712}]" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Be( @"{""code"":83,""result"":null,""correlationId"":null}" );
                    UnsafeHandler.LastUserInfo.Should().Be( "Yes! Albert 3712 is logged in." );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""NO WAY!"",""ActorId"":7}]" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""code"":86,""result"":[""SimpleCrisResultError"",{""isValidationError"":true,""messages"":[[16,0,""Invalid actor identifier: the command provided identifier doesn*t match the current authentication.""]],""logKey"":""*""}],""correlationId"":null}" );
                }
                await s.LogoutAsync();
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""NO! Albert is no more here."",""ActorId"":3712}]" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""code"":86,""result"":[""SimpleCrisResultError"",{""isValidationError"":true,""messages"":[[16,0,""Invalid actor identifier: the command provided identifier doesn*t match the current authentication.""]],""logKey"":""*""}],""correlationId"":null}" );
                }
            }
        }

    }
}
