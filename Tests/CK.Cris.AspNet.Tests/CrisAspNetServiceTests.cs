
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

// Object definition are in "Other" namespace: this tests that the generated code
// is "CK.Cris" namespace independent.
namespace Other
{
    using CK.Core;
    using CK.Cris;
    using System;

    /// <summary>
    /// Test command is in "Other" namespace.
    /// </summary>
    [ExternalName( "Test" )]
    public interface ITestCommand : ICommand
    {
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        int Value { get; set; }
    }

    public class TestHandler : IAutoService
    {
        public static bool Called;

        [CommandHandler]
        public void Execute( ITestCommand cmd )
        {
            Called = true;
        }

        [CommandHandlingValidator]
        public void Validate( UserMessageCollector c, ITestCommand cmd )
        {
            if( cmd.Value <= 0 ) c.Error( "Value must be positive." );
        }
    }

    public class BuggyValidator : IAutoService
    {
        [CommandHandlingValidator]
        public void ValidateCommand( UserMessageCollector c, ITestCommand cmd )
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
    using System.Linq;

    [TestFixture]
    public class CrisAspNetServiceTests
    {
        [Test]
        public async Task basic_call_to_a_command_handler_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ITestCommand ), typeof( TestHandler ), typeof( CrisExecutionContext ) );
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
                    result.ToString().Should().Be( @"{""Result"":null,""ValidationMessages"":null,""CorrelationId"":null}" );
                }
                // Value: 0 is invalid.
                {
                    TestHandler.Called = false;
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri+"?UseSimpleError", @"[""Test"",{""Value"":0}]" );
                    Throw.DebugAssert( r != null );
                    TestHandler.Called.Should().BeFalse( "Validation error." );

                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""Result"":[""AspNetCrisResultError"",{""IsValidationError"":true,""Errors"":[""Value must be positive.""],""LogKey"":""*""}],""ValidationMessages"":[[16,""Value must be positive."",0]],""CorrelationId"":null}" );
                }
            }
        }

        [Test]
        public async Task when_there_is_no_CommandHandler_it_is_directly_an_Execution_error_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ITestCommand ), typeof( BuggyValidator ) );
            using( var s = new CrisTestHostServer( c ) )
            {
                HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError", @"[""Test"",{""Value"":3712}]" );
                Throw.DebugAssert( r != null );
                var result = await s.GetCrisResultAsync( r );
                result.ValidationMessages.Should().BeNull( "Since there is no handler, there's no validation at all." );
                Throw.DebugAssert( result.Result != null );
                var resultError = (IAspNetCrisResultError)result.Result;
                resultError.IsValidationError.Should().BeFalse();    
            }
        }

        [Test]
        public async Task exceptions_raised_by_validators_are_handled_Async()
        {
            // To leak all exceptions in messages, CoreApplicationIdentity must be initialized and be in "#Dev" environment name.  
            CoreApplicationIdentity.Initialize();

            var c = TestHelper.CreateStObjCollector( typeof( ITestCommand ), typeof( BuggyValidator ), typeof( TestHandler ) );
            using( var s = new CrisTestHostServer( c ) )
            {
                HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri+ "?UseSimpleError", @"[""Test"",{""Value"":3712}]" );
                Throw.DebugAssert( r != null );
                var result = await s.GetCrisResultAsync( r );
                result.CorrelationId.Should().NotBeNullOrWhiteSpace();
                Throw.DebugAssert( result.ValidationMessages != null );
                result.ValidationMessages[0].Message.Should().Match( "An unhandled error occurred while validating command 'Test' (LogKey: *)." );
                result.ValidationMessages[1].Message.Should().Match( "This should not happen!" );
                // The ValidationMessages are the same as the ICrisAspNetResultError.
                Throw.DebugAssert( result.Result != null );
                var resultError = (IAspNetCrisResultError)result.Result;
                resultError.IsValidationError.Should().BeTrue();
                resultError.Errors.Should().BeEquivalentTo( result.ValidationMessages.Select( m => m.Message ) );
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
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError", "" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Be( @"{""Result"":[""AspNetCrisResultError"",{""IsValidationError"":true,""Errors"":[""Unable to read Command Poco from empty request body.""],""LogKey"":null}],""ValidationMessages"":[[16,""Unable to read Command Poco from empty request body."",0]],""CorrelationId"":null}" );
                }
                // Here SimpleErrorResult.LogKey is set.
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError", "----" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""Result"":[""AspNetCrisResultError"",{""IsValidationError"":true,""Errors"":[""Unable to read Command Poco from request body (byte length = 4).""],""LogKey"":""*""}],""ValidationMessages"":[[16,""Unable to read Command Poco from request body (byte length = 4)."",0]],""CorrelationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError", "\"X\"" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""Result"":[""AspNetCrisResultError"",{""IsValidationError"":true,""Errors"":[""Unable to read Command Poco from request body (byte length = 3).""],""LogKey"":""*""}],""ValidationMessages"":[[16,""Unable to read Command Poco from request body (byte length = 3)."",0]],""CorrelationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError", "{}" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""Result"":[""AspNetCrisResultError"",{""IsValidationError"":true,""Errors"":[""Unable to read Command Poco from request body (byte length = 2).""],""LogKey"":""*""}],""ValidationMessages"":[[16,""Unable to read Command Poco from request body (byte length = 2)."",0]],""CorrelationId"":null}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError", @"[""Unknown"",{""value"":3712}]" );
                    Throw.DebugAssert( r != null );
                    var result = await s.GetCrisResultWithCorrelationIdSetToNullAsync( r );
                    result.ToString().Should().Match( @"{""Result"":[""AspNetCrisResultError"",{""IsValidationError"":true,""Errors"":[""Unable to read Command Poco from request body (byte length = 26).""],""LogKey"":""*""}],""ValidationMessages"":[[16,""Unable to read Command Poco from request body (byte length = 26)."",0]],""CorrelationId"":null}" );
                }
            }
        }

    }
}
