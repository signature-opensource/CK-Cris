using CK.Core;
using FluentAssertions;
using NUnit.Framework;
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
        public async Task basic_call()
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
                    response.Should().Be( @"[""CrisResult"",{""Code"":83,""Result"":null}]" );
                }
                // Value: 0 is invalid.
                {
                    TestHandler.Called = false;
                    HttpResponseMessage? r = await s.Client.PostJSON( CrisTestServer.CrisUri, @"[""Test"",{""Value"":0}]" );
                    Debug.Assert( r != null );
                    TestHandler.Called.Should().BeFalse( "Validation error." );
                    r.StatusCode.Should().Be( HttpStatusCode.BadRequest );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Be( @"[""CrisResult"",{""Code"":86,""Result"":[""L\u003Cstring\u003E"",[""Value must be positive.""]]}]" );
                }
            }
        }
    }
}
