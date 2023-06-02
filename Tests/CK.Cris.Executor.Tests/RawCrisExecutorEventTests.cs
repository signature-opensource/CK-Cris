using CK.Core;
using NUnit.Framework;
using System.Threading.Tasks;

namespace CK.Cris.Executor.Tests
{
    [TestFixture]
    public class RawCrisExecutorEventTests
    {
        public interface ITestEvent : IEvent
        {
        }

        public class EventSyncHandler : IAutoService
        {
            public static bool Called;

            [RoutedEventHandler]
            public void HandleCommand( ITestEvent e )
            {
                Called = true;
            }
        }

        public class EventAsyncHandler : IAutoService
        {
            [CommandHandler( AllowUnclosedCommand = true )]
            public Task HandleCommandAsync( ITestEvent e )
            {
                EventSyncHandler.Called = true;
                return Task.CompletedTask;
            }
        }

        public class EventValueTaskAsyncHandler : IAutoService
        {
            [CommandHandler]
            public ValueTask HandleCommandAsync( ITestEvent e )
            {
                EventSyncHandler.Called = true;
                return ValueTask.CompletedTask;
            }
        }

    }
}
