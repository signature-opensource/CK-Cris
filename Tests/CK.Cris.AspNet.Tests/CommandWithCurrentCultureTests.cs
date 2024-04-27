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
            /// Gets or sets whether this is a valid incoming command.
            /// When false, the command will not be validated by the [CommandIncomingValidator].
            /// </summary>
            public bool IsIncomingValid { get; set; }

            /// <summary>
            /// Gets or sets whether this is a valid command.
            /// When false, the command will not be validated by the [CommandHandlingValidator].
            /// </summary>
            public bool IsHandlingValid { get; set; }
        }

        public class OneHandler : IAutoService
        {
            [CommandHandler]
            public string Execute( ITestCommand cmd, CurrentCultureInfo culture )
            {
                return culture.CurrentCulture.Name;
            }

            [CommandIncomingValidator]
            public void IncomingValidate( UserMessageCollector c, ITestCommand cmd, CurrentCultureInfo culture )
            {
                c.Info( $"The collector is '{c.Culture}' The current is '{culture.CurrentCulture}'.", "Test.Info" );
                if( !cmd.IsIncomingValid ) c.Error( $"Sorry, this command is INCOMING invalid!", "Test.InvalidIncomingCommand" );
            }

            [CommandHandlingValidator]
            public void HandlingValidate( UserMessageCollector c, ITestCommand cmd, CurrentCultureInfo culture )
            {
                Throw.DebugAssert( c.CurrentCultureInfo == culture );
                if( !cmd.IsHandlingValid ) c.Error( $"Sorry, this command is HANDLING invalid!", "Test.InvalidHandlingCommand" );
            }
        }


        [Test]
        public async Task command_with_no_current_culture_uses_the_english_default_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ITestCommand ), typeof( OneHandler ) );
            using( var s = new CrisTestHostServer( c ) )
            {
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError", @"[""TestCommand"",{""CurrentCultureName"":null,""IsIncomingValid"":true,""IsHandlingValid"":true}]" );
                    string response = await r.Content.ReadAsStringAsync();
                    var result = s.PocoDirectory.Find<IAspNetCrisResult>()!.ReadJson( response );
                    Throw.DebugAssert( result != null );
                    result.Result.Should().Be( "en" );
                    result.ValidationMessages.Should().HaveCount( 1 )
                            .And.Contain( new SimpleUserMessage( UserMessageLevel.Info, "The collector is 'en' The current is 'en'.", 0 ) );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError", @"[""TestCommand"",{""CurrentCultureName"":null,""IsIncomingValid"":false}]" );
                    string response = await r.Content.ReadAsStringAsync();
                    var result = s.PocoDirectory.Find<IAspNetCrisResult>()!.ReadJson( response );
                    Throw.DebugAssert( result != null );
                    result.ValidationMessages.Should().HaveCount( 2 )
                            .And.Contain( new SimpleUserMessage( UserMessageLevel.Info, "The collector is 'en' The current is 'en'.", 0 ) )
                            .And.Contain( new SimpleUserMessage( UserMessageLevel.Error, "Sorry, this command is INCOMING invalid!", 0 ) );
                    result.Result.Should().BeAssignableTo<IAspNetCrisResultError>();
                }
            }
        }

        [Test]
        public async Task command_with_culture_Async()
        {
            NormalizedCultureInfo.GetNormalizedCultureInfo( "fr" ).SetCachedTranslations( new[] {
                ("Test.Info", "Le validateur est en '{0}', la culture courante en '{1}'."),
                ("Test.InvalidIncomingCommand", "Désolé, INCOMING invalide."),
                ("Test.InvalidHandlingCommand", "Désolé, HANDLING invalide."),
            } );
            var c = TestHelper.CreateStObjCollector( typeof( ITestCommand ), typeof( OneHandler ) );
            using( var s = new CrisTestHostServer( c ) )
            {
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError",
                        """["TestCommand",{"CurrentCultureName":"fr","IsIncomingValid":true,"IsHandlingValid":true}]""" );

                    string response = await r.Content.ReadAsStringAsync();
                    var result = s.PocoDirectory.Find<IAspNetCrisResult>()!.ReadJson( response );
                    Throw.DebugAssert( result != null );
                    result.Result.Should().Be( "fr" );
                    result.ValidationMessages.Should().HaveCount( 1 )
                            .And.Contain( new SimpleUserMessage( UserMessageLevel.Info, "Le validateur est en 'fr', la culture courante en 'en'.", 0 ) );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError",
                        """["TestCommand",{"CurrentCultureName":"fr","IsIncomingValid":false}]""" );
                    string response = await r.Content.ReadAsStringAsync();
                    var result = s.PocoDirectory.Find<IAspNetCrisResult>()!.ReadJson( response );
                    Throw.DebugAssert( result != null );
                    result.ValidationMessages.Should().HaveCount( 2 )
                            .And.Contain( new SimpleUserMessage( UserMessageLevel.Info, "Le validateur est en 'fr', la culture courante en 'en'.", 0 ) )
                            .And.Contain( new SimpleUserMessage( UserMessageLevel.Error, "Désolé, INCOMING invalide.", 0 ) );
                    result.Result.Should().BeAssignableTo<IAspNetCrisResultError>();
                    var e = (IAspNetCrisResultError)result.Result!;
                    e.Errors.Should().ContainSingle( "Désolé, INCOMING invalide." );
                }
                {
                    HttpResponseMessage? r = await s.Client.PostJSONAsync( CrisTestHostServer.CrisUri + "?UseSimpleError",
                        """["TestCommand",{"CurrentCultureName":"fr","IsIncomingValid":true,"IsHandlingValid":false}]""" );
                    string response = await r.Content.ReadAsStringAsync();
                    var result = s.PocoDirectory.Find<IAspNetCrisResult>()!.ReadJson( response );
                    Throw.DebugAssert( result != null );
                    result.ValidationMessages.Should().HaveCount( 2 )
                            .And.Contain( new SimpleUserMessage( UserMessageLevel.Info, "Le validateur est en 'fr', la culture courante en 'en'.", 0 ) )
                            .And.Contain( new SimpleUserMessage( UserMessageLevel.Error, "Désolé, HANDLING invalide.", 0 ) );
                    result.Result.Should().BeAssignableTo<IAspNetCrisResultError>();
                    var e = (IAspNetCrisResultError)result.Result!;
                    e.Errors.Should().ContainSingle( "Désolé, HANDLING invalide." );
                }
            }
        }
    }
}
