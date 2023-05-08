using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CK.Cris.AspNet
{
    public partial class CrisAspNetService
    {
        sealed class CrisExecutionContext : ICrisExecutionContext
        {
            readonly CrisAspNetService _s;
            readonly IActivityMonitor _monitor;
            readonly IServiceProvider _services;
            readonly Stack<IAbstractCommand> _commands;
            List<IEvent>? _events;

            public CrisExecutionContext( IAbstractCommand rootCommand, CrisAspNetService s, IActivityMonitor monitor, IServiceProvider services )
            {
                _s = s;
                _monitor = monitor;
                _services = services;
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
                    return _s.DispatchEventAsync( this, e );
                }
                return Task.CompletedTask;
            }

            public Task EmitEventAsync<T>( Action<T> configure ) where T : IEvent
            {
                return EmitEventAsync( _s._poco.Create( configure ) );
            }

            public Task<object?> ExecuteCommandAsync( IAbstractCommand command )
            {
                throw new NotImplementedException();
            }

            public Task<object?> ExecuteCommandAsync<T>( Action<T> configure ) where T : IAbstractCommand
            {
                return ExecuteCommandAsync( _s._poco.Create<T>( configure ) );
            }
        }

        Task DispatchEventAsync( CrisExecutionContext crisCallContext, IEvent e )
        {
            throw new NotImplementedException();
        }
    }
}
