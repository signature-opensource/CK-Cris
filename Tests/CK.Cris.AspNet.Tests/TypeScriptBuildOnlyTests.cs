using CK.Core;
using CK.Testing;
using FluentAssertions;
using NUnit.Framework;
using System.IO;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.AspNet.E2ETests
{
    [TestFixture]
    public class TypeScriptBuildOnlyTests
    {
        [Test]
        public void DiamondResultAndCommand_works()
        {
            var targetOutputPath = TestHelper.GetTypeScriptGeneratedOnlyTargetProjectPath();

            var configuration = TestHelper.CreateDefaultEngineConfiguration( compileOption: Setup.CompileOption.None );
            configuration.FirstBinPath.Types.Add( typeof( CrisAspNetService ),
                                                  typeof( Cris.Tests.IWithTheResultUnifiedCommand ),
                                                  typeof( Cris.Tests.IUnifiedResult ) );
            configuration.FirstBinPath.EnsureTypeScriptConfigurationAspect( targetOutputPath, typeof( Cris.Tests.IWithTheResultUnifiedCommand ) );
            configuration.RunSuccessfully();

            var fCommand = targetOutputPath.Combine( "ck-gen/CK/Cris/Tests/WithPocoResultCommand.ts" );
            var fResult = targetOutputPath.Combine( "ck-gen/CK/Cris/Tests/Result.ts" );

            File.Exists( fCommand ).Should().BeTrue();
            File.Exists( fResult ).Should().BeTrue();
        }

    }
}
