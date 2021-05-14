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

    }
}
