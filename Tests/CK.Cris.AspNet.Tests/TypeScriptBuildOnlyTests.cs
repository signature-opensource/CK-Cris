using CK.Core;
using CK.Cris.AspNet;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using System.IO;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

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
            TestHelper.GenerateTypeScript( targetOutputPath,
                                           new[] {  typeof( CrisAspNetService ),
                                                    typeof( Cris.Tests.IWithTheResultUnifiedCommand ),
                                                    typeof( Cris.Tests.IUnifiedResult ) },
                                           new[] { typeof( Cris.Tests.IWithTheResultUnifiedCommand ) } );

            var fCommand = targetOutputPath.Combine( "ck-gen/src/CK/Cris/Tests/CommandWithPocoResult.ts" );
            var fResult = targetOutputPath.Combine( "ck-gen/src/CK/Cris/Tests/Result.ts" );

            File.Exists( fCommand ).Should().BeTrue();
            File.Exists( fResult ).Should().BeTrue();
        }

    }
}
