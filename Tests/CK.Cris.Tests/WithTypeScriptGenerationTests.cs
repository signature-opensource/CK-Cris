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

namespace CK.Cris.Tests
{
    [TestFixture]
    public class WithTypeScriptGenerationTests
    {
        static readonly NormalizedPath _outputFolder = TestHelper.TestProjectFolder.AppendPart( "TestOutput" );

        class MonoCollectorResolver : IStObjCollectorResultResolver
        {
            readonly Type[] _types;

            public MonoCollectorResolver( params Type[] types )
            {
                _types = types;
            }

            public StObjCollectorResult GetUnifiedResult( BinPathConfiguration unified )
            {
                return TestHelper.GetSuccessfulResult( TestHelper.CreateStObjCollector( _types ) );
            }

            public StObjCollectorResult GetSecondaryResult( BinPathConfiguration head, IEnumerable<BinPathConfiguration> all )
            {
                throw new NotImplementedException( "There is only one BinPath: only the unified one is required." );
            }

        }

        static NormalizedPath GenerateTSCode( string testName, params Type[] types )
        {
            var output = TestHelper.CleanupFolder( _outputFolder.AppendPart( testName ), false );
            var config = new StObjEngineConfiguration();
            config.Aspects.Add( new TypeScriptAspectConfiguration() );
            var b = new BinPathConfiguration();
            b.AspectConfigurations.Add( new XElement( "TypeScript", new XElement( "OutputPath", output ) ) );

            config.BinPaths.Add( b );

            var engine = new StObjEngine( TestHelper.Monitor, config );
            engine.Run( new MonoCollectorResolver( types ) ).Should().BeTrue( "StObjEngine.Run worked." );
            Directory.Exists( output ).Should().BeTrue();
            return output;
        }

        [Test]
        public void CommandUnifiedWithTheResult_works()
        {
            var output = GenerateTSCode( "CommandUnifiedWithTheResult_works", typeof( CommandDirectory ), typeof( ICommandUnifiedWithTheResult ), typeof( IUnifiedResult ) );

            var fInterface1 = output.Combine( "CK/Cris/Tests/ICommandUnifiedWithTheResult.ts" );
            var fInterface2 = output.Combine( "CK/Cris/Tests/ICommandWithMorePocoResult.ts" );
            var fInterface3 = output.Combine( "CK/Cris/Tests/ICommandWithAnotherPocoResult.ts" );
            var fInterface4 = output.Combine( "CK/Cris/Tests/ICommandWithPocoResult.ts" );

            var fResult1 = output.Combine( "CK/Cris/Tests/IUnifiedResult.ts" );
            var fResult2 = output.Combine( "CK/Cris/Tests/IMoreResult.ts" );
            var fResult3 = output.Combine( "CK/Cris/Tests/IAnotherResult.ts" );
            var fResult4 = output.Combine( "CK/Cris/Tests/IResult.ts" );

            var fCommand = output.Combine( "Cris/CommandWithPocoResult.ts" );

            File.ReadAllText( fResult3 ).Should().StartWith( "export interface IAnotherResult" ).And.Contain( "AnotherVal" );
        }

    }
}
