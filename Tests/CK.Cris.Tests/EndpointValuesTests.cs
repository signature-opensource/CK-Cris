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
    public class UbiquitousValuesTests
    {
        public interface IInvalid1Command : ICommand
        {
            [UbiquitousValue]
            int NoWay { get; set; }
        }

        public interface IInvalid2Command : ICommand
        {
            [UbiquitousValue]
            string NoWay { get; set; }
        }

        [TestCase( typeof( IInvalid1Command ), "int" )]
        [TestCase( typeof( IInvalid2Command ), "string" )]
        public void UbiquitousValues_must_be_nullable( Type t, string badType )
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), t );
            TestHelper.GetFailedAutomaticServicesConfiguration( c, $"Ubiquitous value '{badType} CK.Cris.Tests.UbiquitousValuesTests.{t.Name}.NoWay' must be nullable. Ubiquitous values must always be nullable." );
        }

        public interface IInvalid1Values : UbiquitousValues.IUbiquitousValues
        {
            int? NoWay { get; set; }
        }

        public interface IInvalid2Values : UbiquitousValues.IUbiquitousValues
        {
            string? NoWay { get; set; }
        }

        [TestCase( typeof( IInvalid1Values ), "int" )]
        [TestCase( typeof( IInvalid2Values ), "string" )]
        public void IUbiquitousValues_properties_must_not_be_nullable( Type t, string badType )
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), t );
            TestHelper.GetFailedAutomaticServicesConfiguration( c, $"IUbiquitousValues properties cannot be nullable: {badType}? NoWay." );
        }
    }
}

