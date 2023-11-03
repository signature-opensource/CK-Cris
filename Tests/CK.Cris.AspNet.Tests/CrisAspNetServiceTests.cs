
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
    using CK.Core;
    using static CK.Testing.StObjEngineTestHelper;

    [TestFixture]
    public class CrisAspNetServiceTests
    {
        [Test]
        public async Task basic_call_to_a_command_handler_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ICmdTest ), typeof( TestHandler ), typeof( CrisExecutionContext ) );
            using( var s = new CrisTestHostServer( c ) )
            {
                // Value: 3712 is fine (it must be positive).
                {
                    TestHandler.Called = false;
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri, @"[""Test"",{""Value"":3712}]" );
                    Throw.DebugAssert( r != null );
                    TestHandler.Called.Should().BeTrue();

                    string typedResponse = await r.Content.ReadAsStringAsync();
                    typedResponse.Should().StartWith( @"{""result"":null," );

                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Be( @"{""result"":null,""correlationId"":null}" );
                }
                // Value: 0 is invalid.
                {
                    TestHandler.Called = false;
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri, @"[""Test"",{""Value"":0}]" );
                    Throw.DebugAssert( r != null );
                    TestHandler.Called.Should().BeFalse( "Validation error." );

                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""result"":[""AspNetCrisResultError"",{""isValidationError"":true,""messages"":[[16,""Value must be positive."",0]],""logKey"":""*""}],""correlationId"":null}" );
                }
            }
        }

        [Test]
        public async Task exceptions_raised_by_validators_are_handled_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ICmdTest ), typeof( BuggyValidator ) );
            using( var s = new CrisTestHostServer( c ) )
            {
                HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri, @"[""Test"",{""Value"":3712}]" );
                Throw.DebugAssert( r != null );
                var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                result.ToString().Should().Match( @"{""result"":[""AspNetCrisResultError"",{""isValidationError"":true,""messages"":[[16,""An unhandled error occurred while validating command *Test* (LogKey: *)."",0],[16,""This should not happen!"",0]],""logKey"":""*""}],""correlationId"":null}" );
            }
        }

        [Test]
        public async Task bad_request_are_validation_error_Async()
        {
            var c = TestHelper.CreateStObjCollector();
            using( var s = new CrisTestHostServer( c ) )
            {
                // SimpleErrorResult.LogKey is null for really empty input.
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri, "" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Be( @"{""result"":[""AspNetCrisResultError"",{""isValidationError"":true,""messages"":[[16,""Unable to read Command Poco from empty request body."",0]],""logKey"":null}],""correlationId"":null}" );
                }
                // Here SimpleErrorResult.LogKey is set.
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri, "----" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""result"":[""AspNetCrisResultError"",{""isValidationError"":true,""messages"":[[16,""Unable to read Command Poco from request body (byte length = 4)."",0]],""logKey"":""*""}],""correlationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri, "\"X\"" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""result"":[""AspNetCrisResultError"",{""isValidationError"":true,""messages"":[[16,""Unable to read Command Poco from request body (byte length = 3)."",0]],""logKey"":""*""}],""correlationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri, "{}" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""result"":[""AspNetCrisResultError"",{""isValidationError"":true,""messages"":[[16,""Unable to read Command Poco from request body (byte length = 2)."",0]],""logKey"":""*""}],""correlationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri, @"[""Unknown"",{""value"":3712}]" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""result"":[""AspNetCrisResultError"",{""isValidationError"":true,""messages"":[[16,""Unable to read Command Poco from request body (byte length = 26)."",0]],""logKey"":""*""}],""correlationId"":null}" );
                }
            }
        }

    }
}
