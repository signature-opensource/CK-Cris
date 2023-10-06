using CK.AppIdentity;
using CK.Core;
using CK.Cris.AmbientValues;
using CK.Cris.AspNet;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.HttpSender.Tests
{
    [TestFixture]
    public class SenderTests
    {
        [Test]
        public async Task sending_commands_Async()
        {
            await using var runningServer = await TestHelper.RunAspNetServerAsync(
                                                new[] { typeof( CrisAspNetService ),
                                                        typeof( IBeautifulWithOptionsCommand ),
                                                        typeof( AmbientValuesService ),
                                                        typeof( ColorService ),
                                                        typeof( WithOptionsService ) } );

            var serverAddress = runningServer.ServerAddress;

            var callerServices = new[] { typeof( IBeautifulWithOptionsCommand ),
                                         typeof( CrisDirectory ),
                                         typeof( PocoJsonSerializer ),
                                         typeof( ApplicationIdentityService ),
                                         typeof( CrisHttpSenderFeatureDriver )};

            await using var runningCaller = await CreateRunningCallerAsync( runningServer.ServerAddress, callerServices );
            var callerPoco = runningCaller.Services.GetRequiredService<PocoDirectory>();
            var sender = runningCaller.ApplicationIdentityService.Remotes
                                                    .Single( r => r.PartyName == "$Server" )
                                                    .GetRequiredFeature<CrisHttpSender>();
            var cmd = callerPoco.Create<IBeautifulCommand>( c =>
            {
                c.Color = "Black";
                c.Beauty = "Marvellous";
            } );
            var result = await sender.SendAsync( TestHelper.Monitor, cmd );

            result.Result.Should().Be( "Black - Marvellous - 0" );
        }

        [Test]
        public async Task retry_strategy_Async()
        {
            var callerServices = new[] { typeof( IBeautifulWithOptionsCommand ),
                                         typeof( CrisDirectory ),
                                         typeof( PocoJsonSerializer ),
                                         typeof( ApplicationIdentityService ),
                                         typeof( ApplicationIdentityServiceConfiguration ),
                                         typeof( CrisHttpSenderFeatureDriver )};
            await using var runningCaller = await CreateRunningCallerAsync( "http://[::1]:65036/", callerServices );
            var callerPoco = runningCaller.Services.GetRequiredService<PocoDirectory>();
            var sender = runningCaller.ApplicationIdentityService.Remotes
                                                    .Single( r => r.PartyName == "$Server" )
                                                    .GetRequiredFeature<CrisHttpSender>();
            var cmd = callerPoco.Create<IBeautifulCommand>( c =>
            {
                c.Color = "Black";
                c.Beauty = "Marvellous";
            } );

            using( TestHelper.Monitor.CollectTexts( out var logs ) )
            {
                var result = await sender.SendAsync( TestHelper.Monitor, cmd );
                logs.Should()
                    .Contain( """Sending ["CK.Cris.HttpSender.Tests.IBeautifulCommand",{"beauty":"Marvellous","waitTime":0,"color":"Black"}] to 'Domain/$Server/#Dev'.""" )
                    .And.Contain( """Request failed on 'Domain/$Server/#Dev' (attempt n°0).""" )
                    .And.Contain( """Request failed on 'Domain/$Server/#Dev' (attempt n°1).""" )
                    .And.Contain( """Request failed on 'Domain/$Server/#Dev' (attempt n°2).""" )
                    .And.Contain( """While sending: ["CK.Cris.HttpSender.Tests.IBeautifulCommand",{"beauty":"Marvellous","waitTime":0,"color":"Black"}]""" );
            }
        }


        static Task<ApplicationIdentityTestHelperExtension.RunningAppIdentity> CreateRunningCallerAsync( string serverAddress,
                                                                                                         Type[] registerTypes,
                                                                                                         Action<MutableConfigurationSection>? configuration = null )
        {
            var callerCollector = TestHelper.CreateStObjCollector( registerTypes );
            var callerMap = TestHelper.CompileAndLoadStObjMap( callerCollector ).Map;

            return TestHelper.CreateRunningAppIdentityServiceAsync(
                c =>
                {
                    c["FullName"] = "Domain/$Caller";
                    c["Parties:0:FullName"] = "Domain/$Server";
                    c["Parties:0:Address"] = serverAddress;
                    c["Parties:0:CrisHttpSender"] = "true";
                    configuration?.Invoke( c );
                },
                services =>
                {
                    services.AddStObjMap( TestHelper.Monitor, callerMap );
                } );
        }

    }
}
