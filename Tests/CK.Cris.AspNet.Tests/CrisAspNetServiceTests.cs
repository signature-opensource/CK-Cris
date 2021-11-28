using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.AspNet.Tests
{
    [TestFixture]
    public class CrisAspNetServiceTests
    {
        [ExternalName( "Test" )]
        public interface ICmdTest : ICommand
        {
            int Value { get; set; }
        }

        public class TestHandler : IAutoService
        {
            public static bool Called;

            [CommandHandler]
            public void Execute( ICmdTest cmd )
            {
                Called = true;
            }

            [CommandValidator]
            public void Validate( IActivityMonitor m, ICmdTest cmd )
            {
                if( cmd.Value <= 0 ) m.Error( "Value must be positive." );
            }
        }

        [Test]
        public async Task basic_call_to_a_command_handler()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ICmdTest ), typeof( TestHandler ) );
            using( var s = new CrisTestServer( c ) )
            {
                // Value: 3712 is fine (it must be positive).
                {
                    TestHandler.Called = false;
                    HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, @"[""Test"",{""Value"":3712}]" );
                    Debug.Assert( r != null );
                    TestHandler.Called.Should().BeTrue();
                    r.EnsureSuccessStatusCode();
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Be( @"[""CrisResult"",{""code"":83,""result"":null}]" );
                }
                // Value: 0 is invalid.
                {
                    TestHandler.Called = false;
                    HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, @"[""Test"",{""Value"":0}]" );
                    Debug.Assert( r != null );
                    TestHandler.Called.Should().BeFalse( "Validation error." );
                    r.StatusCode.Should().Be( HttpStatusCode.BadRequest );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Be( @"[""CrisResult"",{""code"":86,""result"":[""CrisSimpleError"",{""errors"":[""Value must be positive.""]}]}]" );
                }
            }
        }

        public class BuggyValidator : IAutoService
        {
            [CommandValidator]
            public void ValidateCommand( IActivityMonitor m, ICmdTest cmd )
            {
                throw new Exception( "This should not happen!" );
            }
        }

        [Test]
        public async Task exceptions_raised_by_validators_are_handled_and_results_to_a_Code_E_and_an_HttpStatusCode_InternalServerError()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ICmdTest ), typeof( BuggyValidator ) );
            using( var s = new CrisTestServer( c ) )
            {
                HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, @"[""Test"",{""Value"":3712}]" );
                Debug.Assert( r != null );
                r.StatusCode.Should().Be( HttpStatusCode.InternalServerError );
                string response = await r.Content.ReadAsStringAsync();
                response.Should().Be( @"[""CrisResult"",{""code"":69,""result"":[""CrisSimpleError"",{""errors"":[""CommandValidator unexpected error."",""This should not happen!""]}]}]" );
            }
        }

        [Test]
        public async Task invalid_json_or_unknown_command_are_bad_request_with_detailed_error()
        {
            var c = TestHelper.CreateStObjCollector();
            using( var s = new CrisTestServer( c ) )
            {
                {
                    HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, @"[""Unknown"",{""value"":3712}]" );
                    Debug.Assert( r != null );
                    r.StatusCode.Should().Be( HttpStatusCode.BadRequest );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Be( @"[""CrisResult"",{""code"":86,""result"":[""CrisSimpleError"",{""errors"":[""Unable to read Command Poco from request body (byte length = 26)."",""Poco type \u0027Unknown\u0027 not found.""]}]}]" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, "" );
                    Debug.Assert( r != null );
                    r.StatusCode.Should().Be( HttpStatusCode.BadRequest );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Be( @"[""CrisResult"",{""code"":86,""result"":[""CrisSimpleError"",{""errors"":[""Unable to read Command Poco from empty request body.""]}]}]" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, "{}" );
                    Debug.Assert( r != null );
                    r.StatusCode.Should().Be( HttpStatusCode.BadRequest );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Be( @"[""CrisResult"",{""code"":86,""result"":[""CrisSimpleError"",{""errors"":[""Unable to read Command Poco from request body (byte length = 2)."",""Expecting Json Poco array.""]}]}]" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, "----" );
                    Debug.Assert( r != null );
                    r.StatusCode.Should().Be( HttpStatusCode.BadRequest );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().StartWith( @"[""CrisResult"",{""code"":86,""result"":[""CrisSimpleError"",{""errors"":[""Unable to read Command Poco from request body (byte length = 4)."",""" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, "\"X\"" );
                    Debug.Assert( r != null );
                    r.StatusCode.Should().Be( HttpStatusCode.BadRequest );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().StartWith( @"[""CrisResult"",{""code"":86,""result"":[""CrisSimpleError"",{""errors"":[""Unable to read Command Poco from request body (byte length = 3)."",""" );
                }
            }
        }

    }
}
