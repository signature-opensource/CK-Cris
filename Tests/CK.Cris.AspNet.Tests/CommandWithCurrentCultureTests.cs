using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using Other;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.AspNet.Tests
{
    [TestFixture]
    public class CommandWithCurrentCultureTests
    {
        [ExternalName( "TestCommand" )]
        public interface ITestCommand : ICommand<string>, ICommandWithCurrentCulture
        {
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
                    response.Should().Match( @"{""result"":""en"",""validationMessages"":null,""correlationId"":""*""}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri, @"[""TestCommand"",{""CurrentCultureName"":null,""IsValid"":false}]" );
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
                    response.Should().Match( @"{""result"":""fr"",""validationMessages"":null,""correlationId"":""*""}" );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri, @"[""TestCommand"",{""CurrentCultureName"":null,""IsValid"":false}]" );
                    string response = await r.Content.ReadAsStringAsync();
                    response.Should().Match( @"{""result"":[""AspNetCrisResultError"",{""isValidationError"":true,""errors"":[""Sorry, this command is invalid!""],""logKey"":""*""}],""validationMessages"":[[4,""The current culture is en."",0],[16,""Sorry, this command is invalid!"",0]],""correlationId"":""*""}" );
                }
            }
        }
    }
}
