using CK.Core;
using CK.Cris.AspNet;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using System.IO;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.TypeScript.Tests
{
    [TestFixture]
    public class E2ETests
    {
        public interface IColoredAmbientValues : AmbientValues.IAmbientValues
        {
            string Color { get; set; }
        }

        public class ColorService : IAutoService
        {
            [CommandPostHandler]
            public void GetColoredAmbientValues( AmbientValues.IAmbientValuesCollectCommand cmd, IColoredAmbientValues values )
            {
                values.Color = "Red";
            }

            [CommandHandler]
            public string HandleBeatifulCommand( IBeautifulCommand cmd )
            {
                return $"{cmd.Color} - {cmd.Beauty}";
            }
        }

        public interface ICommandColored : ICommandPart
        {
            /// <summary>
            /// Gets or sets the color.
            /// <para>
            /// This is an ambient value: the caller can set it but if not, the CrisEndPoint transparently
            /// sets it.
            /// </para>
            /// </summary>
            string Color { get; set; }
        }

        public interface IBeautifulCommand : ICommandColored, ICommand<string>
        {
            /// <summary>
            /// Gets or sets the beauty's string.
            /// </summary>
            string Beauty { get; set; }
        }

        [Test]
        public async Task AmbientValues_works_Async()
        {
            var targetOutputPath = TestHelper.GetTypeScriptWithTestsSupportTargetProjectPath();
            await TestHelper.RunAspNetE2ETestAsync( targetOutputPath,
                                                    new[] { // By registering the IBeautifulCommand first,
                                                            // we use (and test!) the fact that the OnPocoGenerating calls EnsurePoco
                                                            // on the IAmbientValues so that the ambient values are known when handling
                                                            // the first command...
                                                            typeof( IBeautifulCommand ),
                                                            typeof( AmbientValues.AmbientValuesService ),
                                                            typeof( IColoredAmbientValues ),
                                                            typeof( ColorService ),
                                                            typeof( CrisAspNetService ) },
                                                    new[] { typeof( IBeautifulCommand ) } );
        }

    }
}
