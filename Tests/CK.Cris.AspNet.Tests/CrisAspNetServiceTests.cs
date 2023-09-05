
// Object definition are in "Other" namespace: this tests that the generated code
// is "CK.Cris" namespace independent.
namespace Other
{
    using CK.Core;
    using CK.Cris;
    using System;

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
        public void Validate( UserMessageCollector c, ICmdTest cmd )
        {
            if( cmd.Value <= 0 ) c.Error( "Value must be positive." );
        }
    }

    public class BuggyValidator : IAutoService
    {
        [CommandValidator]
        public void ValidateCommand( UserMessageCollector c, ICmdTest cmd )
        {
            throw new Exception( "This should not happen!" );
        }
    }


}

namespace CK.Cris.AspNet.Tests
{
    using Other;
    using FluentAssertions;
    using NUnit.Framework;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading.Tasks;
    using static CK.Testing.StObjEngineTestHelper;

    [TestFixture]
    public class CrisAspNetServiceTests
    {
        [Test]
        public async Task basic_call_to_a_command_handler_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ICmdTest ), typeof( TestHandler ), typeof( CrisExecutionContext ) );
            using( var s = new CrisTestServer( c ) )
            {
                // Value: 3712 is fine (it must be positive).
                {
                    TestHandler.Called = false;
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""Test"",{""Value"":3712}]" );
                    Debug.Assert( r != null );
                    TestHandler.Called.Should().BeTrue();

                    string typedResponse = await r.Content.ReadAsStringAsync();
                    typedResponse.Should().StartWith( @"[""CrisResult"",{" );

                    var result = await s.GetCrisResultWithNullCorrelationIdAsync( r );
                    result.ToString().Should().Be( @"{""code"":83,""result"":null,""correlationId"":null}" );
                }
                // Value: 0 is invalid.
                {
                    TestHandler.Called = false;
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""Test"",{""Value"":0}]" );
                    Debug.Assert( r != null );
                    TestHandler.Called.Should().BeFalse( "Validation error." );

                    var result = await s.GetCrisResultWithNullCorrelationIdAsync( r );
                    result.ToString().Should().Be( @"{""code"":86,""result"":[""CrisResultError"",{""errors"":[""Value must be positive.""]}],""correlationId"":null}" );
                }
            }
        }

        [Test]
        public async Task exceptions_raised_by_validators_are_handled_and_results_to_a_Code_E_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ICmdTest ), typeof( BuggyValidator ) );
            using( var s = new CrisTestServer( c ) )
            {
                HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""Test"",{""Value"":3712}]" );
                Debug.Assert( r != null );
                var result = await s.GetCrisResultWithNullCorrelationIdAsync( r );
                result.ToString().Should().Be( @"{""code"":69,""result"":[""CrisResultError"",{""errors"":[""CommandValidator unexpected error."",""This should not happen!""]}],""correlationId"":null}" );
            }
        }

        [Test]
        public async Task invalid_json_or_unknown_command_are_bad_request_with_detailed_error_Async()
        {
            var c = TestHelper.CreateStObjCollector();
            using( var s = new CrisTestServer( c ) )
            {
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""Unknown"",{""value"":3712}]" );
                    Debug.Assert( r != null );
                    var result = await s.GetCrisResultWithNullCorrelationIdAsync( r );
                    result.ToString().Should().Be( @"{""code"":86,""result"":[""CrisResultError"",{""isValidationError"":false,""messages"":[[16,0,""Unable to read Command Poco from request body (byte length = 26)."",""en-us"",""Cris.AspNet.ReadCommandFailed"",""Unable to read Command Poco from request body (byte length = 26)."",""en-us"",[61,2]]],""logKey"":null}],""correlationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, "" );
                    Debug.Assert( r != null );
                    var result = await s.GetCrisResultWithNullCorrelationIdAsync( r );
                    result.ToString().Should().Be( @"{""code"":86,""result"":[""CrisResultError"",{""isValidationError"":false,""messages"":[[16,0,""Unable to read Command Poco from empty request body."",""en-us"",""Cris.AspNet.EmptyBody"",""Unable to read Command Poco from empty request body."",""en-us"",[]]],""logKey"":null}],""correlationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, "{}" );
                    Debug.Assert( r != null );
                    var result = await s.GetCrisResultWithNullCorrelationIdAsync( r );
                    result.ToString().Should().Be( @"{""code"":86,""result"":[""CrisResultError"",{""isValidationError"":false,""messages"":[[16,0,""Unable to read Command Poco from request body (byte length = 2)."",""en-us"",""Cris.AspNet.ReadCommandFailed"",""Unable to read Command Poco from request body (byte length = 2)."",""en-us"",[61,1]]],""logKey"":null}],""correlationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, "----" );
                    Debug.Assert( r != null );
                    var result = await s.GetCrisResultWithNullCorrelationIdAsync( r );
                    result.ToString().Should().Be( @"{""code"":86,""result"":[""CrisResultError"",{""isValidationError"":false,""messages"":[[16,0,""Unable to read Command Poco from request body (byte length = 4)."",""en-us"",""Cris.AspNet.ReadCommandFailed"",""Unable to read Command Poco from request body (byte length = 4)."",""en-us"",[61,1]]],""logKey"":null}],""correlationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, "\"X\"" );
                    Debug.Assert( r != null );
                    var result = await s.GetCrisResultWithNullCorrelationIdAsync( r );
                    result.ToString().Should().Be( @"{""code"":86,""result"":[""CrisResultError"",{""isValidationError"":false,""messages"":[[16,0,""Unable to read Command Poco from request body (byte length = 3)."",""en-us"",""Cris.AspNet.ReadCommandFailed"",""Unable to read Command Poco from request body (byte length = 3)."",""en-us"",[61,1]]],""logKey"":null}],""correlationId"":null}" );
                }
            }
        }

    }
}
