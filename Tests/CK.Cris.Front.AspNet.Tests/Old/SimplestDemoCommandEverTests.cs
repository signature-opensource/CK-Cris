using CK.Core;
using CK.Cris;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        [CommandName("Demo", "Demonstration", "Sample", "Sample" )]
        public interface IDemoCommand : ICommand
        {
            string Signal { get; set; }
        }

        /// <summary>
        /// The simplest possible handler (aynchronous only) for <see cref="IDemoCommand"/>.
        /// No logging, no dependency of any kind.
        /// </summary>
        public class DemoHandler : IAutoService
        {
            public readonly static ConcurrentQueue<string> Called = new ConcurrentQueue<string>();

            /// <summary>
            /// The simplest (required) signature.
            /// </summary>
            /// <param name="command">The command object.</param>
            /// <returns>The awaitable.</returns>
            public Task HandleAsync( IDemoCommand c )
            {
                Called.Enqueue( $"Asynchronous Signal: {c.Signal}" );
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
            /// The simplest (required) signature for a handler.
            /// </summary>
            /// <param name="command">The command object.</param>
            public void Handle( IDemoCommand c )
            {
                Called.Enqueue( $"Synchronous Signal: {c.Signal}" );
            }
        }

        ///// <summary>
        ///// This must be automatically implemented.
        ///// This is a marked as ISingletonService here for clarity but this is useless: since DemoHandler has
        ///// no dependency, it is singleton, hence this one is too.
        ///// </summary>
        //public class ManualCommandReceiverOnBaseAsyncHandler : ISingletonAutoService
        //{
        //    readonly DemoHandler _handler;

        //    public ManualCommandReceiverOnBaseAsyncHandler( DemoHandler handler )
        //    {
        //        _handler = handler;
        //    }

        //    public bool IsFakeSync => true;

        //    public bool IsFakeAsync => false;

        //    public CommandResult Handle( IServiceProvider sp, IDemoCommand command )
        //    {
        //        return HandleAsync( sp, command ).ConfigureAwait( false ).GetAwaiter().GetResult();
        //    }

        //    public async Task<CommandResult> HandleAsync( IServiceProvider sp, IDemoCommand command )
        //    {
        //        await _handler.HandleAsync( command );
        //        return new CommandResult( command, null );
        //    }
        //}

        //[AttributeUsage(AttributeTargets.Interface)]
        //public class CommandNameAttribute : Attribute
        //{
        //    public CommandNameAttribute( string name )
        //    {
        //        Name = name;
        //    }

        //    public string Name { get; }
        //}


        //public interface ICommandModel
        //{
        //    string CommandName { get; }

        //    Type CommandType { get; }
        //}

        //public abstract class CommandRegistry : ISingletonAutoService
        //{
        //    public abstract IReadOnlyDictionary<string,ICommandModel> Commands { get; }
        //}


        //[Test]
        //public void receiving_and_handling_the_demo_command_with_the_base_handler()
        //{
        //    var c = TestHelper.CreateStObjCollector( typeof( FrontCommandExecutor ), typeof( IDemoCommand ) );
        //    var services = TestHelper.GetAutomaticServices( c ).Services;
        //    services.GetService<FrontCommandExecutor>().Should().NotBeNull();
        //}
    }
}
