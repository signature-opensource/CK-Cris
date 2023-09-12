using CK.Setup;
using CK.Core;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.TypeScript.Tests
{
    static class TypeScriptTestHelper
    {
        public static readonly NormalizedPath OutputFolder = TestHelper.TestProjectFolder.AppendPart( "TypeScriptOutput" );

        public static NormalizedPath GetOutputFolder( [CallerMemberName] string? testName = null )
        {
            return TestHelper.CleanupFolder( OutputFolder.AppendPart( testName ), false );
        }

        /// <summary>
        /// Simple, mono bin path, implementation that uses the TestHelper to collect the StObj
        /// based on an explicit list of types.
        /// </summary>
        public class MonoCollectorResolver : IStObjCollectorResultResolver
        {
            readonly Type[] _types;

            public MonoCollectorResolver( IEnumerable<Type> types )
            {
                _types = types.Append( typeof( CrisDirectory ) )
                              .Append( typeof( TypeScriptCrisCommandGenerator ) )
                              .Append( typeof( CK.Core.PocoJsonSerializer ) )
                              .ToArray();
            }

            public StObjCollectorResult? GetResult( RunningBinPathGroup g )
            {
                return TestHelper.GetSuccessfulResult( TestHelper.CreateStObjCollector( _types ) );
            }
        }

        public static (NormalizedPath OutputPath, NormalizedPath SourcePath) GenerateTSCode( string testName, params Type[] types )
        {
            return GenerateTSCode( testName, new MonoCollectorResolver( types ) );
        }

        public static (NormalizedPath OutputPath, NormalizedPath SourcePath) GenerateTSCode( string testName, TypeScriptAspectConfiguration tsConfig, params Type[] types )
        {
            return GenerateTSCode( testName, tsConfig, new MonoCollectorResolver( types ) );
        }

        public static (NormalizedPath OutputPath, NormalizedPath SourcePath) GenerateTSCode( string testName, IStObjCollectorResultResolver collectorResults )
        {
            return GenerateTSCode( testName, new TypeScriptAspectConfiguration(), collectorResults );
        }

        public static (NormalizedPath OutputPath, NormalizedPath SourcePath) GenerateTSCode( string testName, TypeScriptAspectConfiguration tsConfig, IStObjCollectorResultResolver collectorResults )
        {
            NormalizedPath output = GetOutputFolder( testName );
            var config = new StObjEngineConfiguration();
            config.Aspects.Add( tsConfig );
            var b = new BinPathConfiguration();
            b.AspectConfigurations.Add( new XElement( "TypeScript", new XElement( "OutputPath", output ) ) );
            config.BinPaths.Add( b );

            var engine = new StObjEngine( TestHelper.Monitor, config );
            engine.Run( collectorResults ).Success.Should().BeTrue( "StObjEngine.Run worked." );
            Directory.Exists( output ).Should().BeTrue();
            return (output, output.AppendPart( "ts" ).AppendPart( "src" ));
        }
    }
}
