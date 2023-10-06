using CK.Core;
using NUnit.Framework;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.AspNet.E2ETests
{
    [TestFixture]
    public class TSTests
    {
        public interface IColoredAmbientValues : AmbientValues.IAmbientValues
        {
            /// <summary>
            /// The color of <see cref="ICommandColored"/> commands.
            /// </summary>
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
            /// This is an ambient value: the caller can set it but if not, the TypeScript
            /// CrisEndPoint transparently sets it.
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
        public async Task E2ETest_Async()
        {
            var targetOutputPath = TestHelper.GetTypeScriptWithTestsSupportTargetProjectPath();
            Throw.DebugAssert( targetOutputPath.EndsWith( "/TSTests/E2ETest_Async" ) );
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
                                                    new[] { typeof( IBeautifulCommand ) },
                                                    resume =>
                                                    resume );
        }

    }
}
