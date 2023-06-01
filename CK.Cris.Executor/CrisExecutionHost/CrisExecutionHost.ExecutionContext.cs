using CK.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Cris
{
    public sealed partial class CrisExecutionHost
    {
        sealed class ExecutionContext : ICrisExecutionContext
        {
            readonly CrisExecutionHost _host;
            readonly IActivityMonitor _monitor;
            readonly TheTruc _truc;
            readonly Stack<IAbstractCommand> _commands;
            List<IEvent>? _events;

            public ExecutionContext( IActivityMonitor monitor,
                                     CrisExecutionHost host ,
                                     TheTruc truc,
                                     IAbstractCommand rootCommand )
            {
                _host = host;
                _monitor = monitor;
                _truc = truc;
                _commands = new Stack<IAbstractCommand>();
                _commands.Push( rootCommand );
            }

            public IActivityMonitor Monitor => _monitor;

            public Task EmitEventAsync( IEvent e )
            {
                _events ??= new List<IEvent>();
                _events.Add( e );
                if( e is IEventWithCommand c ) c.SourceCommand = _commands.Peek();
                if( e is IEventGlobal d ) d.PartyFullName = CoreApplicationIdentity.IsInitialized
                                                                ? CoreApplicationIdentity.Instance.FullName
                                                                : $"{CoreApplicationIdentity.DefaultDomainName}/{CoreApplicationIdentity.DefaultEnvironmentName}/{CoreApplicationIdentity.DefaultPartyName}";
                if( e.CrisPocoModel.Kind == CrisPocoKind.RoutedEventImmediate )
                {
                    return _truc.ReturnEventAsync( _monitor, e );
                }
                return Task.CompletedTask;
            }

            public Task EmitEventAsync<T>( Action<T> configure ) where T : IEvent
            {
                return EmitEventAsync( _host._pocoDirectory.Create( configure ) );
            }

            public Task<object?> ExecuteCommandAsync<T>( Action<T> configure ) where T : IAbstractCommand
            {
                return ExecuteCommandAsync( _host._pocoDirectory.Create<T>( configure ) );
            }

            public Task<object?> ExecuteCommandAsync( IAbstractCommand command )
            {
                throw new NotImplementedException();
            }
        }
    }
}
