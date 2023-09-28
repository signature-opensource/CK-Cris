using CK.Core;
using CK.Cris.AspNet;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.TypeScript.Tests
{
    [TestFixture]
    public class TypeScriptGenerationTests
    {
        [Test]
        public void DiamondResultAndCommand_works()
        {
            var targetOutputPath = TestHelper.GetTypeScriptWithTestsSupportTargetProjectPath();
            TestHelper.GenerateTypeScript( targetOutputPath,
                                           new[] {  typeof( CrisAspNetService ),
                                                    typeof( Cris.Tests.ICommandUnifiedWithTheResult ),
                                                    typeof( Cris.Tests.IUnifiedResult ) },
                                           new[] { typeof( Cris.Tests.ICommandUnifiedWithTheResult ) } );

            var fCommand = targetOutputPath.Combine( "ck-gen/src/CK/Cris/Tests/CommandWithPocoResult.ts" );
            var fResult = targetOutputPath.Combine( "ck-gen/src/CK/Cris/Tests/Result.ts" );

            File.Exists( fCommand ).Should().BeTrue();
            File.Exists( fResult ).Should().BeTrue();
        }

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
        }

        public interface ICommandColored : ICommandPart
        {
            /// <summary>
            /// Gets or sets the Nico's color.
            /// </summary>
            string Color { get; set; }
        }

        public interface IBeautifulCommand : ICommandColored
        {
            /// <summary>
            /// Gets or sets the beauty's string.
            /// </summary>
            string Beauty { get; set; }
        }

        [Test]
        public void beautiful_colored_command_with_ambient_values()
        {
            var targetOutputPath = TestHelper.GetTypeScriptWithTestsSupportTargetProjectPath();
            TestHelper.GenerateTypeScript( targetOutputPath,
                                   new[] { // By registering the IBeautifulCommand first,
                                           // we use (and test!) the fact that the OnPocoGenerating calls EnsurePoco
                                           // on the IAmbientValues so that the ambient values are known when handling
                                           // the first command...
                                           typeof( IBeautifulCommand ),
                                           typeof( AmbientValues.IAmbientValues ),
                                           typeof( AmbientValues.IAmbientValuesCollectCommand ),
                                           typeof( AmbientValues.AmbientValuesService ),
                                           typeof( IColoredAmbientValues ),
                                           typeof( ColorService ),
                                           typeof( ICommandColored ),
                                           typeof( CrisAspNetService ) },
                                   new[] { typeof( IBeautifulCommand ) } );
        }

    }
}
