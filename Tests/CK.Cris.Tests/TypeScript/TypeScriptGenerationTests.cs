using CK.Core;
using CK.Setup;
using CK.Text;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.Tests
{
    [TestFixture]
    public class TypeScriptGenerationTests
    {
        [Test]
        public void DiamondResultAndCommand_works()
        {
            var output = TypeScriptTestHelper.GenerateTSCode( nameof( DiamondResultAndCommand_works ),
                                                              typeof( CommandDirectory ),
                                                              typeof( ICommandUnifiedWithTheResult ),
                                                              typeof( IUnifiedResult ) );

            var fCommand = output.Combine( "CK/Cris/Tests/CommandWithPocoResult.ts" );
            var fResult = output.Combine( "CK/Cris/Tests/Result.ts" );

            var command = File.ReadAllText( fCommand );
            var result = File.ReadAllText( fResult );
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
            string Color { get; set; }
        }

        public interface IBeautifulCommand : ICommandColored
        {
            string Beauty { get; set; }
        }

        [Test]
        public void with_ambient_values()
        {
            var output = TypeScriptTestHelper.GenerateTSCode( nameof( with_ambient_values ),
                                                              typeof( CommandDirectory ),
                                                              typeof( AmbientValues.IAmbientValues ),
                                                              typeof( AmbientValues.IAmbientValuesCollectCommand ),
                                                              typeof( AmbientValues.AmbientValuesService ),
                                                              typeof( IColoredAmbientValues ),
                                                              typeof( ColorService ),
                                                              typeof( ICommandColored ),
                                                              typeof( IBeautifulCommand ) );

        }

    }
}
