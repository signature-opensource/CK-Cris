using CK.Core;
using CK.Cris;
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
            public Task HandleAsync( ReceivedCommand<IDemoCommand> c )
            {
                Called.Enqueue( $"Signal: {c.Command.Signal}, {c.ToString()}" );
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
            public void Handle( ReceivedCommand<IDemoCommand> c )
            {
                Called.Enqueue( $"Signal: {c.Command.Signal}, {c.ToString()}" );
            }
        }

        /// <summary>
        /// This must be automatically implemented.
        /// This is a marked as ISingletonService here for clarity but we totally don't care!
        /// </summary>
        public class ManualCommandReceiverOnBaseAsyncHandler : ICommandReceiver<IDemoCommand>, ISingletonAutoService
        {
            readonly DemoHandler _handler;

            public ManualCommandReceiverOnBaseAsyncHandler( DemoHandler handler )
            {
                _handler = handler;
            }

            public bool IsFakeSync => true;

            public bool IsFakeAsync => false;

            public VISAMResponse Handle( IServiceProvider sp, ReceivedCommand<IDemoCommand> command )
            {
                return HandleAsync( sp, command ).ConfigureAwait( false ).GetAwaiter().GetResult();
            }

            public async Task<VISAMResponse> HandleAsync( IServiceProvider sp, ReceivedCommand<IDemoCommand> command )
            {
                await _handler.HandleAsync( command );
                return new VISAMResponse( command, null );
            }

            // It is useless to try/catch here, foreach receiver: it is more clever to do it once in the End Point.
            //
            //public async Task<VISAMResponse> HandleAsync( IServiceProvider sp, ReceivedCommand<IDemoCommand> command )
            //{
            //    try
            //    {
            //        await _handler.HandleAsync( command );
            //        return new VISAMResponse( command, null );
            //    }
            //    catch( Exception ex )
            //    {
            //        return new VISAMResponse( command, CKExceptionData.CreateFrom( ex ) );
            //    }
            //}
        }

        //public class CommandEndPointService : ISingletonAutoService
        //{
        //    public Task<VISAMResponse> HandleAsync( IActivityMonitor m, object command )
        //    {

        //    }
        //}

        [AttributeUsage(AttributeTargets.Interface)]
        public class CommandNameAttribute : Attribute
        {
            public CommandNameAttribute( string name )
            {
                Name = name;
            }

            public string Name { get; }
        }

        //public class CommandModel 
        //{
        //    public CommandModel( Type t )
        //    {
        //        var namedAttr = t.GetCustomAttributesData()
        //                            .Where( attr => typeof(CommandNameAttribute).IsAssignableFrom( attr.AttributeType ) )
        //                            .FirstOrDefault()?
        //                            .ConstructorArguments[0].Value;

        //    }

        //    public string CommandName { get; }

        //    public Type CommandType { get; }
        //}

        public interface ICommandModel
        {
            string CommandName { get; }

            Type CommandType { get; }
        }

        public abstract class CommandRegistry : ISingletonAutoService
        {
            []
            public abstract IReadOnlyDictionary<string,ICommandModel> Commands { get; }
        }



        [Test]
        public void receiving_and_handling_the_demo_command_with_the_base_handler()
        {
            var c = TestHelper.CreateStObjCollector( typeof( DemoHandler ), typeof( IDemoCommand ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;

            services.GetService<IPocoFactory<IDemoCommand>>().Should().NotBeNull( "The command Poco has been registered." );
            services.GetService<ICommandHandler<IDemoCommand>>().Should().NotBeNull( "The command handler is available." );



        }
    }
}
