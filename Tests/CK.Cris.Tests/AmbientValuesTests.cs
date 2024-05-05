using CK.Core;
using CK.Cris.AmbientValues;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.Tests
{
    [TestFixture]
    public class AmbientValuesTests
    {
        public interface IInvalid1Command : ICommand
        {
            [AmbientServiceValue]
            int NoWay { get; set; }
        }

        public interface IInvalid2Command : ICommand
        {
            [AmbientServiceValue]
            string NoWay { get; set; }
        }

        [TestCase( typeof( IInvalid1Command ), "int" )]
        [TestCase( typeof( IInvalid2Command ), "string" )]
        public void AmbientValues_must_be_nullable( Type t, string badType )
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), t );
            TestHelper.GetFailedAutomaticServicesConfiguration( c, $"[AmbientServiceValue] '{badType} CK.Cris.Tests.AmbientValuesTests.{t.Name}.NoWay' must be nullable. Ambient values must always be nullable." );
        }

        public interface IInvalid1Values : IAmbientValues
        {
            int? NoWay { get; set; }
        }

        public interface IInvalid2Values : IAmbientValues
        {
            string? NoWay { get; set; }
        }

        [TestCase( typeof( IInvalid1Values ), "int" )]
        [TestCase( typeof( IInvalid2Values ), "string" )]
        public void IAmbientValues_properties_must_not_be_nullable( Type t, string badType )
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), t );
            TestHelper.GetFailedAutomaticServicesConfiguration( c, $"IAmbientValues properties cannot be nullable: {badType}? NoWay." );
        }

        public interface IAmNotCrisPoco : IPoco
        {
            [AmbientServiceValue]
            string? NoWay { get; set; }
        }

        [Test]
        public void AmbientValues_properties_can_only_appear_in_Cris_Poco_Types()
        {
            // Add the IAmbientValuesCollectCommand so that there is at least one Poco otherwise
            // Poco handling is skipped.
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ), typeof( IAmNotCrisPoco ), typeof( IAmbientValuesCollectCommand ) );
            TestHelper.GetFailedAutomaticServicesConfiguration( c, "Invalid [AmbientServiceValue] 'CK.Cris.Tests.AmbientValuesTests.IAmNotCrisPoco.NoWay' on PrimaryPoco. Only ICrisPoco properties can be AmbientService values." );
        }

        public interface IAmCommand : ICommand
        {
            [AmbientServiceValue]
            string? V1 { get; set; }
        }

        public interface IAmEvent : IEvent
        {
            [AmbientServiceValue]
            int? V2 { get; set; }
        }

        public interface ITestAmbientValues : IAmbientValues
        {
            string V1 { get; set; }
            int V2 { get; set; }
        }

        [Test]
        public void AmbientValues_properties_must_match_IAmbiantValues_properties_Types()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisDirectory ),
                                                     typeof( IAmCommand ),
                                                     typeof( IAmEvent ),
                                                     typeof( ITestAmbientValues ),
                                                     typeof( IAmbientValuesCollectCommand ) );
            using var services = TestHelper.CreateAutomaticServices( c ).Services;
        }


    }
}

