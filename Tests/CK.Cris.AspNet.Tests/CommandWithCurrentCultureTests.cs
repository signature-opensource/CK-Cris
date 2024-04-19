using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System.Net.Http;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.AspNet.Tests
{
    [TestFixture]
    public class CommandWithCurrentCultureTests
    {
        /// <summary>
        /// Secondary <see cref="ICommandWithCurrentCulture"/> that adds a IsValid property.
        /// </summary>
        [ExternalName( "TestCommand" )]
        public interface ITestCommand : ICommand<string>, ICommandWithCurrentCulture
        {
            /// <summary>
            /// Gets or sets whether this is valid.
            /// When false, the command will not be valid.
            /// </summary>
            public bool IsValid { get; set; }
        }

        public class OneHandler : IAutoService
        {
            [CommandHandler]
            public string Execute( ITestCommand cmd, CurrentCultureInfo culture )
            {
                return culture.CurrentCulture.Name;
            }

            [CommandValidator]
            public void Validate( UserMessageCollector c, ITestCommand cmd, CurrentCultureInfo culture )
            {
                Throw.DebugAssert( c.CurrentCulture == culture );
                c.Info( $"The current culture is {culture.CurrentCulture.Name}." );
                if( !cmd.IsValid ) c.Error( $"Sorry, this command is invalid!", "Test.InvalidCommand" );
            }
        }


        [Test]
        public async Task command_with_no_current_culture_uses_the_english_default_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ITestCommand ), typeof( OneHandler ) );
            using( var s = new CrisTestHostServer( c ) )
            {
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri, @"[""TestCommand"",{""CurrentCultureName"":null,""IsValid"":true}]" );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Match( @"{""result"":[""string"",""en""],""validationMessages"":null,""correlationId"":""*""}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError", @"[""TestCommand"",{""CurrentCultureName"":null,""IsValid"":false}]" );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Match( @"{""result"":[""AspNetCrisResultError"",{""isValidationError"":true,""errors"":[""Sorry, this command is invalid!""],""logKey"":""*""}],""validationMessages"":[[4,""The current culture is en."",0],[16,""Sorry, this command is invalid!"",0]],""correlationId"":""*""}" );
                }
            }
        }

        [Test]
        public async Task command_with_culture_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ITestCommand ), typeof( OneHandler ) );
            using( var s = new CrisTestHostServer( c ) )
            {
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri, @"[""TestCommand"",{""CurrentCultureName"":""fr"",""IsValid"":true}]" );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Match( @"{""result"":[""string"",""fr""],""validationMessages"":null,""correlationId"":""*""}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError", @"[""TestCommand"",{""CurrentCultureName"":null,""IsValid"":false}]" );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Match( @"{""result"":[""AspNetCrisResultError"",{""isValidationError"":true,""errors"":[""Sorry, this command is invalid!""],""logKey"":""*""}],""validationMessages"":[[4,""The current culture is en."",0],[16,""Sorry, this command is invalid!"",0]],""correlationId"":""*""}" );
                }
            }
        }
    }
}
