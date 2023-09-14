
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
    using CK.Core;

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
                    Throw.DebugAssert( r != null );
                    TestHandler.Called.Should().BeTrue();

                    string typedResponse = await r.Content.ReadAsStringAsync();
                    typedResponse.Should().StartWith( @"[""CrisResult"",{" );

                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Be( @"{""code"":83,""result"":null,""correlationId"":null}" );
                }
                // Value: 0 is invalid.
                {
                    TestHandler.Called = false;
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""Test"",{""Value"":0}]" );
                    Throw.DebugAssert( r != null );
                    TestHandler.Called.Should().BeFalse( "Validation error." );

                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""code"":86,""result"":[""SimpleCrisResultError"",{""isValidationError"":true,""messages"":[[16,0,""Value must be positive.""]],""logKey"":""*""}],""correlationId"":null}" );
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
                Throw.DebugAssert( r != null );
                var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                result.ToString().Should().Match( @"{""code"":86,""result"":[""SimpleCrisResultError"",{""isValidationError"":true,""messages"":[[16,0,""An unhandled error occurred while validating command *Test* (LogKey: *).""]],""logKey"":""*""}],""correlationId"":null}" );
            }
        }

        [Test]
        public async Task invalid_json_or_unknown_command_are_bad_request_with_detailed_error_Async()
        {
            var c = TestHelper.CreateStObjCollector();
            using( var s = new CrisTestServer( c ) )
            {
                // SimpleErrorResult.LogKey is null for really empty input.
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, "" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Be( @"{""code"":86,""result"":[""SimpleCrisResultError"",{""isValidationError"":false,""messages"":[[16,0,""Unable to read Command Poco from empty request body.""]],""logKey"":null}],""correlationId"":null}" );
                }
                // Here SimpleErrorResult.LogKey is set.
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, "----" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""code"":86,""result"":[""SimpleCrisResultError"",{""isValidationError"":false,""messages"":[[16,0,""Unable to read Command Poco from request body (byte length = 4).""]],""logKey"":""*""}],""correlationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, "\"X\"" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""code"":86,""result"":[""SimpleCrisResultError"",{""isValidationError"":false,""messages"":[[16,0,""Unable to read Command Poco from request body (byte length = 3).""]],""logKey"":""*""}],""correlationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, "{}" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""code"":86,""result"":[""SimpleCrisResultError"",{""isValidationError"":false,""messages"":[[16,0,""Unable to read Command Poco from request body (byte length = 2).""]],""logKey"":""*""}],""correlationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestServer.CrisUri, @"[""Unknown"",{""value"":3712}]" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""code"":86,""result"":[""SimpleCrisResultError"",{""isValidationError"":false,""messages"":[[16,0,""Unable to read Command Poco from request body (byte length = 26).""]],""logKey"":""*""}],""correlationId"":null}" );
                }
            }
        }

    }
}
