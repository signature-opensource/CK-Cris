using CK.Core;
using CK.Testing;
using FluentAssertions;
using NUnit.Framework;
using System.IO;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.AspNet.Tests;

[TestFixture]
public class DiamondResultAndCommandTests
{
    [Test]
    [Ignore( "This fails with 'Cannot find module 'axios' or its corresponding type declarations.' but WHY???" )]
    public async Task DiamondResultAndCommand_works_Async()
    {
        var targetOutputPath = TestHelper.GetTypeScriptNpmPackageTargetProjectPath();

        var configuration = TestHelper.CreateDefaultEngineConfiguration( compileOption: Setup.CompileOption.None );
        configuration.FirstBinPath.Types.Add( typeof( CrisAspNetService ),
                                              typeof( Cris.Tests.IWithTheResultUnifiedCommand ),
                                              typeof( Cris.Tests.IUnifiedResult ) );
        configuration.FirstBinPath.EnsureTypeScriptConfigurationAspect( targetOutputPath, typeof( Cris.Tests.IWithTheResultUnifiedCommand ) );
        await configuration.RunSuccessfullyAsync();

        var fCommand = targetOutputPath.Combine( "ck-gen/src/CK/Cris/Tests/WithPocoResultCommand.ts" );
        var fResult = targetOutputPath.Combine( "ck-gen/src/CK/Cris/Tests/Result.ts" );

        File.Exists( fCommand ).Should().BeTrue();
        File.Exists( fResult ).Should().BeTrue();
    }

}
