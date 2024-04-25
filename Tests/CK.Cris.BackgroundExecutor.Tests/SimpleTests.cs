using CK.Auth;
using CK.Core;
using FluentAssertions;
using FluentAssertions.Common;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

using static CK.Testing.StObjEngineTestHelper;

namespace CK.Cris.BackgroundExecutor.Tests
{

    [TestFixture]
    public class SimpleTests
    {
        static readonly List<string> Traces = new List<string>();
        [AllowNull]
        ServiceProvider _services;

        static public string SafeTrace( string t )
        {
            lock( Traces ) { Traces.Add( t ); }
            return t;
        }

        public interface IDelayCommand : ICommand
        {
            string Name { get; set; }

            [DefaultValue( -1 )]
            int Delay { get; set; }
        }

        public sealed class RegularScopedService : IScopedAutoService, IDisposable
        {
            readonly IActivityMonitor _monitor;

            public RegularScopedService( IActivityMonitor monitor )
            {
                _monitor = monitor;
                monitor.Info( $"Instantiating {nameof(RegularScopedService)}." );
            }

            public void Dispose()
            {
                _monitor.Info( $"Disposing {nameof( RegularScopedService )}." );
            }
        }

        public sealed class StupidHandlers : ISingletonAutoService
        {
            [CommandHandler]
            public async Task HandleCommandAsync( IActivityMonitor monitor, IDelayCommand command, RegularScopedService scopedOne )
            {
                scopedOne.Should().NotBeNull();
                monitor.Trace( SafeTrace( $"In '{command.Name}'." ) );
                var d = command.Delay;
                if( d != 0 )
                {
                    if( d < 0 ) d = Random.Shared.Next( 0, 100 );
                    await Task.Delay( d );
                }
                monitor.Trace( SafeTrace( $"Out '{command.Name}'." ) );
            }
        }

        [OneTimeSetUp]
        public async Task OneTimeSetUpAsync()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CrisBackgroundExecutorService ),
                                                     typeof( StdAuthenticationTypeSystem ),
                                                     typeof( IDelayCommand ),
                                                     typeof( StupidHandlers ),
                                                     typeof( CrisExecutionContext ),
                                                     typeof( RegularScopedService ) );
            _services = await TestHelper.StartHostedServicesAsync( TestHelper.CreateAutomaticServices( c ).Services );
            _services.GetRequiredService<CrisExecutionHost>().ParallelRunnerCount = 1;
        }

        [OneTimeTearDown]
        public async Task OneTimeDearDownAsync()
        {
            await _services.DisposeAsync();
        }

        [Test]
        public async Task executing_commands_Async()
        {
            using( var scope = _services.CreateScope() )
            {
                Traces.Clear();
                var poco = scope.ServiceProvider.GetRequiredService<PocoDirectory>();
                var back = scope.ServiceProvider.GetRequiredService<CrisBackgroundExecutorService>();
                var ubiq = scope.ServiceProvider.GetRequiredService<AmbientServiceHub>();
                var commands = Enumerable.Range( 0, 20 )
                                         .Select( i => back.Submit( TestHelper.Monitor, poco.Create<IDelayCommand>( c => c.Name = i.ToString() ), ubiq ) );
                TestHelper.Monitor.Info( "Waiting for commands to be executed." );
                await Task.WhenAll( commands.Select( c => c.SafeCompletion ) );
                var all = Traces.Concatenate();
                var expected = Enumerable.Range( 0, 20 )
                                         .Select( i => $"In '{i}'., Out '{i}'." )
                                         .Concatenate();
                all.Should().Be( expected );
            }
        }

        [Test]
        public async Task executing_with_2_runners_Async()
        {
            using( var scope = _services.CreateScope() )
            {
                Traces.Clear();
                var poco = _services.GetRequiredService<PocoDirectory>();
                var back = _services.GetRequiredService<CrisBackgroundExecutorService>();
                var ubiq = scope.ServiceProvider.GetRequiredService<AmbientServiceHub>();
                back.ExecutionHost.ParallelRunnerCount = 2;

                // The runner count is done asynchronously. The second runner may not kick in
                // fast enough.
                await Task.Delay( 15 );

                var commands = Enumerable.Range( 0, 20 )
                                         .Select( i => back.Submit( TestHelper.Monitor, poco.Create<IDelayCommand>( c => c.Name = i.ToString() ), ubiq ) );
                TestHelper.Monitor.Info( "Waiting for the commands to be executed." );
                await Task.WhenAll( commands.Select( c => c.SafeCompletion ) );
                Traces.Should().HaveCount( 2 * 20 );

                var all = Traces.Concatenate();
                var expected = Enumerable.Range( 0, 20 )
                                         .Select( i => $"In '{i}'., Out '{i}'." )
                                         .Concatenate();
                all.Should().NotBe( expected );
            }
        }

        [Test]
        public async Task executing_with_4_runners_Async()
        {
            using( var scope = _services.CreateScope() )
            {
                Traces.Clear();
                var poco = _services.GetRequiredService<PocoDirectory>();
                var back = _services.GetRequiredService<CrisBackgroundExecutorService>();
                var ambientServices = scope.ServiceProvider.GetRequiredService<AmbientServiceHub>();
                back.ExecutionHost.ParallelRunnerCount = 4;

                var commands = Enumerable.Range( 0, 20 )
                                         .Select( i => back.Submit( TestHelper.Monitor, poco.Create<IDelayCommand>( c => c.Name = i.ToString() ), ambientServices ) );
                TestHelper.Monitor.Info( "Waiting for the commands to be executed." );
                await Task.WhenAll( commands.Select( c => c.SafeCompletion ) );
                Traces.Should().HaveCount( 2 * 20 );

                var all = Traces.Concatenate();
                var expected = Enumerable.Range( 0, 20 )
                                         .Select( i => $"In '{i}'., Out '{i}'." )
                                         .Concatenate();
                all.Should().NotBe( expected );
            }
        }

        [TestCase(10)]
        [TestCase(150)]
        public async Task stress_test_runner_count_change_Async( int countChange )
        {
            using( var scope = _services.CreateScope() )
            {
                Traces.Clear();
                var poco = scope.ServiceProvider.GetRequiredService<PocoDirectory>();
                var back = scope.ServiceProvider.GetRequiredService<CrisBackgroundExecutorService>();

                int eventualRunnerCount = Random.Shared.Next( 1, 10 );

                var changer1 = Task.Run( async () =>
                {
                    for( int i = 0; i < countChange; i++ )
                    {
                        back.ExecutionHost.ParallelRunnerCount = Random.Shared.Next( 1, 10 );
                        await Task.Delay( Random.Shared.Next( 40, 60 ) );
                    }
                    back.ExecutionHost.ParallelRunnerCount = eventualRunnerCount;
                } );

                var changer2 = Task.Run( async () =>
                {
                    for( int i = 0; i < countChange; i++ )
                    {
                        back.ExecutionHost.ParallelRunnerCount = Random.Shared.Next( 1, 10 );
                        await Task.Delay( Random.Shared.Next( 40, 60 ) );
                    }
                    back.ExecutionHost.ParallelRunnerCount = eventualRunnerCount;
                } );

                var commands = Task.Run( async () =>
                {
                    for( int i = 0; i < countChange; i++ )
                    {
                        var ambient = scope.ServiceProvider.GetRequiredService<AmbientServiceHub>();
                        await back.Submit( TestHelper.Monitor, poco.Create<IDelayCommand>( c => c.Name = i.ToString() ), ambient ).SafeCompletion;
                    }
                    back.ExecutionHost.ParallelRunnerCount = eventualRunnerCount;
                } );

                await commands;
                await changer1;
                await changer2;

                // If the final delta is important, we need some time for the runner count to stabilize.
                await Task.Delay( 50 );

                back.ExecutionHost.ParallelRunnerCount.Should().Be( eventualRunnerCount );
                Traces.Should().HaveCount( 2 * countChange );
            }
        }

    }
}
