using CK.Core;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.AspNet.Tests
{
    [TestFixture]
    public class CrisAspNetServiceTests
    {
        [ExternalName( "Test" )]
        public interface ICmdTest : ICommand
        {
            int Value { get; set; }
        }



        public void simple()
        {

        }
    }
}
