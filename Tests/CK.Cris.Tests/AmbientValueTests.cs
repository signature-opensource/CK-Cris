using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.Tests
{
    [TestFixture]
    public class AmbientValueTests
    {
        public interface IInvalid1 : ICommand
        {
            [SafeAmbientValue]
            int NoWay { get; set; }
        }

        public interface IInvalid2 : ICommand
        {
            [SafeAmbientValue]
            string NoWay { get; set; }
        }

        [TestCase( typeof( IInvalid1 ), "int" )]
        [TestCase( typeof( IInvalid2 ), "string" )]
        public void AmbienValues_must_be_nullable( Type t, string badType )
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), t );
            using( TestHelper.Monitor.CollectEntries( out var logs ) )
            {
                TestHelper.GenerateCode( c, null ).Success.Should().BeFalse();
                logs.Select( l => l.Text )
                    .Any( e => e == $"Ambient value '{badType} CK.Cris.Tests.AmbientValueTests.{t.Name}.NoWay' must be nullable. Ambient values must always be nullable." )
                    .Should().BeTrue();
            }
        }
    }
}

