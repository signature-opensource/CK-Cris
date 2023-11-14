using CK.Core;
using NUnit.Framework;
using System;
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

        public interface ICommandWithMessage : ICommand<SimpleUserMessage>
        {
        }

        public class ColorAndBuggyService : IAutoService
        {
            [CommandPostHandler]
            public void GetColoredAmbientValues( AmbientValues.IAmbientValuesCollectCommand cmd, IColoredAmbientValues values )
            {
                values.Color = "Red";
            }

            [CommandHandler]
            public string HandleBeautifulCommand( IBeautifulCommand cmd )
            {
                return $"{cmd.Color} - {cmd.Beauty}";
            }

            [CommandHandler]
            public SimpleUserMessage Handle( CurrentCultureInfo culture, ICommandWithMessage cmd )
            {
                return culture.InfoMessage( $"Local servert time is {DateTime.Now}." );
            }

            [CommandValidator]
            public void ValidateBuggyCommand( UserMessageCollector collector, IBuggyCommand cmd )
            {
                if( cmd.EmitValidationError )
                {
                    collector.Error( "The BuggyCommand is not valid (by design)." );
                }
            }

            [CommandHandler]
            public void HandleBuggyCommand( IBuggyCommand cmd )
            {
                Throw.DebugAssert( cmd.EmitValidationError is false );
                throw new System.NotImplementedException( "BuggyCommand handler is not implemented." );
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

        public interface IBuggyCommand : ICommand
        {
            /// <summary>
            /// Gets or sets whether a validation error must be emitted or
            /// a <see cref="System.NotImplementedException"/> must be thrown
            /// from command execution.
            /// </summary>
            bool EmitValidationError { get; set; }
        }

        [Test]
        public async Task E2ETest_Async()
        {
            //
            // When running in Debug, this will wait until resume is set to true.
            // Until then, the .NET server is running and tests can be manually executed
            // written and fixed.
            // 
            // To stop, simply put a breakpoint in the resume lambda and sets the resume value
            // to true with the Watch window.
            //
            // In regular run, this will not wait for resume.
            //
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
                                                            typeof( ColorAndBuggyService ),
                                                            typeof( IBuggyCommand ),
                                                            typeof( ICommandWithMessage ),
                                                            typeof( CrisAspNetService ) },
                                                    new[] { typeof( IBeautifulCommand ), typeof( IBuggyCommand ), typeof( ICommandWithMessage ) },
                                                    resume =>
                                                    resume );
        }

    }
}
