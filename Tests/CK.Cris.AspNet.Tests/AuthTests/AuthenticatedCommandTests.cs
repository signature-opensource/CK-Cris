using CK.Auth;
using CK.Core;
using CK.Testing;
using FluentAssertions;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.AspNet.Tests.AuthTests
{
    [TestFixture]
    public class AuthenticatedCommandTests
    {
        /// <summary>
        /// An unsafe command: when validated or executed, the <see cref="IAuthenticationInfo.UnsafeUser"/> is known
        /// but <see cref="IAuthenticationInfo.User"/> is the anonymous.
        /// <para>
        /// The <see cref="CrisAuthenticationService"/> automatically ensures that the <see cref="IAuthUnsafePart.ActorId"/>
        /// is the one on the currently connected user otherwise, the command is not validated.
        /// </para>
        /// </summary>
        [ExternalName( "UnsafeCommand" )]
        public interface IUnsafeCommand : ICommandAuthUnsafe
        {
            /// <summary>
            /// Gets or sets a string used by the test. When set to "NO",
            /// it means that the command MUST NOT be validated: the handler
            /// never sses it.
            /// </summary>
            string UserInfo { get; set; }
        }

        /// <summary>
        /// Same as <see cref="IUnsafeCommand"/> but with a result that is list of integers.
        /// </summary>
        [ExternalName( "UnsafeWithResultCommand" )]
        public interface IUnsafeWithResultCommand : ICommand<List<int>>, ICommandAuthUnsafe
        {
            string UserInfo { get; set; }
        }

        /// <summary>
        /// Before reaching this handler, 
        /// </summary>
        public class UnsafeHandler : IAutoService
        {
            [CommandHandler]
            public void Execute( IUnsafeCommand cmd )
            {
                cmd.UserInfo.Should().NotStartWith( "NO" );
                LastUserInfo = cmd.UserInfo;
            }

            [CommandHandler]
            public List<int> Execute( IUnsafeWithResultCommand cmd )
            {
                cmd.UserInfo.Should().NotStartWith( "NO" );
                LastUserInfo = cmd.UserInfo;
                return new List<int>() { 42, 3712 };
            }

            static public string? LastUserInfo;
        }

        [Test]
        public async Task ICommandAuthUnsafe_cannot_be_fooled_on_its_ActorId_Async()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IUnsafeCommand ),
                                                  typeof( IUnsafeWithResultCommand ),
                                                  typeof( UnsafeHandler ),
                                                  typeof( CrisExecutionContext ) );
            using( var s = new CrisTestHostServer( configuration.FirstBinPath, withAuthentication: true ) )
            {
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri, @"[""UnsafeCommand"",{""UserInfo"":""YES"",""ActorId"":0}]" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Be( @"{""Result"":null,""ValidationMessages"":null,""CorrelationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError", @"[""UnsafeWithResultCommand"",{""UserInfo"":""YES. There is no ActorId in the Json => it is let to null.""}]" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( """{"Result":["AspNetCrisResultError",{"IsValidationError":true,"Errors":["Invalid property: ActorId cannot be null."],"LogKey":"*"}],"ValidationMessages":[[16,"Invalid property: ActorId cannot be null.",0]],"CorrelationId":null}""" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError", @"[""UnsafeCommand"",{""UserInfo"":""NO WAY!"",""ActorId"":3712}]" );
                    Throw.DebugAssert( r != null );
                    IAspNetCrisResult result = await s.GetCrisResultAsync( r );
                    var correlationId = ActivityMonitor.Token.Parse( result.CorrelationId );
                    string.IsNullOrWhiteSpace( correlationId.OriginatorId ).Should().BeFalse();
                    var error = result.Result as IAspNetCrisResultError;
                    Debug.Assert( error != null );
                    error.IsValidationError.Should().BeTrue();
                    error.Errors.Should().HaveCount( 1 );
                    error.Errors[0].Should().Be( "Invalid actor identifier: the provided identifier doesn't match the current authentication." );
                    var errorLogKey = ActivityMonitor.LogKey.Parse( error.LogKey );
                    string.IsNullOrWhiteSpace( errorLogKey.OriginatorId ).Should().BeFalse();
                    error.ToString().Should().Match( """{"IsValidationError":true,"Errors":["Invalid actor identifier: the provided identifier doesn*t match the current authentication."],"LogKey":"*"}""" );
                }
                UnsafeHandler.LastUserInfo = null;
                await s.LoginAsync( "Albert" );
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri, @"[""UnsafeCommand"",{""userInfo"":""Yes! Albert 3712 is logged in."",""actorId"":3712}]" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Be( @"{""Result"":null,""ValidationMessages"":null,""CorrelationId"":null}" );
                    UnsafeHandler.LastUserInfo.Should().Be( "Yes! Albert 3712 is logged in." );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError", @"[""UnsafeCommand"",{""UserInfo"":""NO WAY!"",""ActorId"":7}]" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( """{"Result":["AspNetCrisResultError",{"IsValidationError":true,"Errors":["Invalid actor identifier: the provided identifier doesn*t match the current authentication."],"LogKey":"*"}],"ValidationMessages":[[16,"Invalid actor identifier: the provided identifier doesn*t match the current authentication.",0]],"CorrelationId":null}""" );
                }
                await s.LogoutAsync();
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError", @"[""UnsafeCommand"",{""UserInfo"":""NO! Albert is no more here."",""ActorId"":3712}]" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""Result"":[""AspNetCrisResultError"",{""IsValidationError"":true,""Errors"":[""Invalid actor identifier: the provided identifier doesn*t match the current authentication.""],""LogKey"":""*""}],""ValidationMessages"":[[16,""Invalid actor identifier: the provided identifier doesn*t match the current authentication."",0]],""CorrelationId"":null}" );
                }
            }
        }

    }
}
