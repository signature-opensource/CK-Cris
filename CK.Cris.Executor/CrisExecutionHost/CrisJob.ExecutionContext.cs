using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Cris
{
    public abstract partial class CrisJob
    {
        internal sealed class ExecutionContext : ICrisExecutionContext
        {
            readonly CrisJob _job;
            readonly CrisExecutionHost _host;
            readonly IActivityMonitor _monitor;

            internal AsyncServiceScope _scoped;

            // Stack is mutable struct based with ref access of the head.
            record struct StackFrame( IAbstractCommand Command, List<IEvent>? Events );
            readonly List<StackFrame> _stack;
            ref StackFrame StackPeek() => ref CollectionsMarshal.AsSpan( _stack )[_stack.Count - 1];
            void StackPop() => _stack.RemoveAt( _stack.Count - 1 );

            StackFrame StackPush( IAbstractCommand command )
            {
                var f = new StackFrame( command, null );
                _stack.Add( f );
                return f;
            }

            public ExecutionContext( CrisJob job, IActivityMonitor runnerMonitor, CrisExecutionHost host )
            {
                _monitor = runnerMonitor;
                _job = job;
                _host = host;
                _stack = new List<StackFrame>();
                StackPush( job.Command );
                // For the DI endpoint.
                job._runnerMonitor = runnerMonitor;
                job._executionContext = this;
            }

            public IActivityMonitor Monitor => _monitor;

            internal IReadOnlyList<IEvent> FinalEvents => (IReadOnlyList<IEvent>?)_stack[0].Events ?? Array.Empty<IEvent>();

            public Task EmitEventAsync( IEvent e )
            {
                ref var frame = ref StackPeek();
                if( e is IEventWithCommand c ) c.SourceCommand = frame.Command;
                if( e is IEventRestrictedParty d ) d.PartyFullName = CoreApplicationIdentity.IsInitialized
                                                                ? CoreApplicationIdentity.Instance.FullName
                                                                : $"{CoreApplicationIdentity.DefaultDomainName}/{CoreApplicationIdentity.DefaultEnvironmentName}/{CoreApplicationIdentity.DefaultPartyName}";
                if( e.CrisPocoModel.Kind == CrisPocoKind.RoutedEventImmediate )
                {
                    return _job._executor.RaiseImmediateEventAsync( _monitor, _job, e );
                }
                else
                {
                    frame.Events ??= new List<IEvent>();
                    frame.Events.Add( e );
                }
                return Task.CompletedTask;
            }

            public Task EmitEventAsync<T>( Action<T> configure ) where T : IEvent
            {
                return EmitEventAsync( _host.PocoDirectory.Create( configure ) );
            }

            public Task<object?> ExecuteCommandAsync<T>( Action<T> configure ) where T : IAbstractCommand
            {
                return ExecuteCommandAsync( _host.PocoDirectory.Create<T>( configure ) );
            }

            public async Task<object?> ExecuteCommandAsync( IAbstractCommand command )
            {
                Throw.CheckNotNullArgument( command );
                var f = StackPush( command );
                var r = await _host._commandExecutor.RawExecuteAsync( _scoped.ServiceProvider, command );
                var e = f.Events;
                StackPop();
                if( e != null ) AppendEvents( e );
                return r;
            }

            void AppendEvents( List<IEvent> events )
            {
                ref var f = ref StackPeek();
                if( f.Events == null ) f.Events = events;
                else f.Events.AddRange( events );
            }

            public async Task<IExecutedCommand<T>> ExecuteAsync<T>( T command, bool stopEventPropagation = false ) where T : class, IAbstractCommand
            {
                Throw.CheckNotNullArgument( command );
                var f = StackPush( command );
                var r = await _host._commandExecutor.RawExecuteAsync( _scoped.ServiceProvider, command );
                var e = f.Events;
                StackPop();
                if( e != null && !stopEventPropagation ) AppendEvents( e );
                return new ExecutedCommand<T>( command, r, e );
            }

            public Task<IExecutedCommand<T>> ExecuteAsync<T>( Action<T> configure, bool stopEventPropagation = false ) where T : class, IAbstractCommand
            {
                return ExecuteAsync( _host.PocoDirectory.Create<T>( configure ), stopEventPropagation );
            }
        }
    }
}
