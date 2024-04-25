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
    public class EndpointValuesTests
    {
        public interface IInvalid1Command : ICommand
        {
            [EndpointValue]
            int NoWay { get; set; }
        }

        public interface IInvalid2Command : ICommand
        {
            [EndpointValue]
            string NoWay { get; set; }
        }

        [TestCase( typeof( IInvalid1Command ), "int" )]
        [TestCase( typeof( IInvalid2Command ), "string" )]
        public void EndpointValues_must_be_nullable( Type t, string badType )
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), t );
            TestHelper.GetFailedAutomaticServicesConfiguration( c, $"Endpoint value '{badType} CK.Cris.Tests.EndpointValuesTests.{t.Name}.NoWay' must be nullable. Endpoint values must always be nullable." );
        }

        public interface IInvalid1Values : EndpointValues.IEndpointValues
        {
            int? NoWay { get; set; }
        }

        public interface IInvalid2Values : EndpointValues.IEndpointValues
        {
            string? NoWay { get; set; }
        }

        [TestCase( typeof( IInvalid1Values ), "int" )]
        [TestCase( typeof( IInvalid2Values ), "string" )]
        public void IEndpointValues_properties_must_not_be_nullable( Type t, string badType )
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), t );
            TestHelper.GetFailedAutomaticServicesConfiguration( c, $"IEndpointValues properties cannot be nullable: {badType}? NoWay." );
        }
    }
}

