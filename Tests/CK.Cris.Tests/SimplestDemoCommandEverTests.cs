using CK.Core;
using CK.Cris;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace Tests
{
    public class SimplestDemoCommandEverTests
    {
        /// <summary>
        /// This command has no result.
        /// This is enough to define a command.
        /// </summary>
        public interface IDemoCommand : ICommand
        {
            string Signal { get; set; }
        }

        /// <summary>
        /// The simplest possible handler (aynchronous only) for <see cref="IDemoCommand"/>.
        /// No logging, no dependency of any kind.
        /// </summary>
        public class DemoHandler : ICommandHandler<IDemoCommand>
        {
            public readonly static ConcurrentQueue<string> Called = new ConcurrentQueue<string>();

            /// <summary>
            /// The simplest (required) signature.
            /// </summary>
            /// <param name="command">The command object.</param>
            /// <returns>The awaitable.</returns>
            public Task HandleAsync( IDemoCommand command )
            {
                Called.Enqueue( command.Signal );
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Extends the initial handler to offer synchronous support.
        /// Still no logging and no dependency of any kind.
        /// </summary>
        public class BetterDemoHandler : DemoHandler
        {
            /// <summary>
            /// The simplest (required) signature.
            /// </summary>
            /// <param name="command">The command object.</param>
            public void Handle( IDemoCommand command )
            {
                Called.Enqueue( command.Signal );
            }
        }

        [Test]
        public void receiving_and_handling_the_democommand_with_the_base_handler()
        {
            var c = TestHelper.CreateStObjCollector( typeof( DemoHandler ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;

            services.GetService<IPocoFactory<IDemoCommand>>().Should().NotBeNull( "The command Poco has been registered." );
            services.GetService<ICommandHandler<IDemoCommand>>().Should().NotBeNull( "The command handler is available." );

        }
    }
}
