using CK.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Cris.DynamicConfiguration;

/// <summary>
/// The dynamic configuration of the App: its run properties (<see cref="RunPropertyDirectory"/>) are initialized when
/// the application starts and changed while it runs (<see cref="Configure"/>, the handler of the
/// <c>IConfigurePropertyCommand</c>).
/// <list type="bullet">
///   <item>
///   <b>The initial value is resolved, then set once</b>: a configured value that changed since a change was recorded
///   wins (the latest decision); otherwise the recorded change; otherwise the configured value. Without any of them, the
///   property keeps the value of its code. A record of a property that can't be changed anymore (restricted by the
///   configuration, or gone) is dropped with a warning.
///   </item>
///   <item>
///   <b>A change</b> compares the current value (the optimistic concurrency), converts the new one, sets it (the setter
///   validates) and records it in the <see cref="IDynamicConfigurationStore"/> (when there is one).
///   </item>
/// </list>
/// The owners of the properties never read the configuration: they are configuration-agnostic.
/// </summary>
public sealed class DynamicConfigurationService : ISingletonAutoService, IHostedService
{
    readonly IServiceProvider _services;
    readonly IConfiguration _configuration;
    readonly RunPropertyDirectory _directory;
    readonly IDynamicConfigurationStore? _store;
    readonly object _lock;
    Dictionary<string, DynamicConfigurationRecord> _records;
    bool _initialized;

    /// <summary>
    /// Initializes the service.
    /// </summary>
    /// <param name="services">The root services (the owners of the properties are singletons).</param>
    /// <param name="configuration">The configuration of the host.</param>
    /// <param name="directory">The run properties.</param>
    /// <param name="store">The optional store of the changes.</param>
    public DynamicConfigurationService( IServiceProvider services,
                                        IConfiguration configuration,
                                        RunPropertyDirectory directory,
                                        IDynamicConfigurationStore? store = null )
    {
        _services = services;
        _configuration = configuration;
        _directory = directory;
        _store = store;
        _lock = new object();
        _records = new Dictionary<string, DynamicConfigurationRecord>( StringComparer.OrdinalIgnoreCase );
    }

    /// <summary>
    /// Gets the run properties of the App (the ones restricted by the configuration included: <see cref="RunProperty.IsRun"/>).
    /// </summary>
    public IReadOnlyList<RunProperty> Properties => _directory.Properties;

    /// <summary>
    /// Gets the current value of a property.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>Its value.</returns>
    public object? GetValue( RunProperty property ) => property.GetValue( _services );

    Task IHostedService.StartAsync( CancellationToken cancellationToken )
    {
        var monitor = new ActivityMonitor( "DynamicConfigurationService.Initialize" );
        try
        {
            Initialize( monitor );
        }
        finally
        {
            monitor.MonitorEnd();
        }
        return Task.CompletedTask;
    }

    Task IHostedService.StopAsync( CancellationToken cancellationToken ) => Task.CompletedTask;

    /// <summary>
    /// Initializes the properties (once: the host calls it when it starts).
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    public void Initialize( IActivityMonitor monitor )
    {
        lock( _lock )
        {
            if( _initialized ) return;
            _initialized = true;
            var records = _store?.Load( monitor ) ?? new Dictionary<string, DynamicConfigurationRecord>();
            bool dropped = false;
            foreach( var (path, record) in records )
            {
                var p = _directory.Find( path );
                if( p == null || !p.IsRun )
                {
                    monitor.Warn( $"The recorded value of '{path}' is dropped: {(p == null ? "it is not a run property of the application anymore" : "the configuration restricts it")}." );
                    dropped = true;
                    continue;
                }
                var configured = _configuration[p.Path];
                if( configured != record.ConfiguredValue )
                {
                    monitor.Info( $"The configured value of '{path}' changed since its value was recorded: the configuration wins (the latest decision)." );
                    dropped = true;
                    continue;
                }
                _records[p.Path] = record;
            }
            foreach( var p in _directory.Properties )
            {
                string? text;
                if( _records.TryGetValue( p.Path, out var r ) ) text = r.Value;
                else
                {
                    text = _configuration[p.Path];
                    if( text == null ) continue;
                }
                if( !RunProperty.TryConvert( text, p.ValueType, out var value, out var error ) )
                {
                    monitor.Error( $"Invalid value for '{p.Path}': {error} The value of the code is kept." );
                    continue;
                }
                try
                {
                    p.SetValue( _services, value );
                }
                catch( Exception ex )
                {
                    monitor.Error( $"While initializing '{p.Path}': the value of the code is kept.", ex );
                }
            }
            if( dropped ) Save( monitor );
        }
    }

    /// <summary>
    /// Changes a run property: see <see cref="DynamicConfigurationService"/>.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="path">The path of the property.</param>
    /// <param name="value">The new value.</param>
    /// <param name="currentValue">The value that the sender believes is the current one.</param>
    /// <param name="message">The message: why it failed, or what changed.</param>
    /// <returns>True on success.</returns>
    public bool Configure( IActivityMonitor monitor, string path, object? value, object? currentValue, out string message )
    {
        var p = _directory.Find( path );
        if( p == null || !p.IsRun )
        {
            message = p == null
                        ? $"'{path}' is not a property that can be changed while the application runs."
                        : $"'{p.Path}' can't be changed while the application runs: the configuration restricts it.";
            return false;
        }
        lock( _lock )
        {
            var actual = p.GetValue( _services );
            if( !RunProperty.TryConvert( currentValue, p.ValueType, out var expected, out _ ) || !Equals( expected, actual ) )
            {
                message = $"The current value of '{p.Path}' is '{RunProperty.ToText( actual ) ?? "null"}', not '{RunProperty.ToText( currentValue ) ?? "null"}'.";
                return false;
            }
            if( !RunProperty.TryConvert( value, p.ValueType, out var v, out var error ) )
            {
                message = error!;
                return false;
            }
            try
            {
                p.SetValue( _services, v );
            }
            catch( Exception ex )
            {
                monitor.Warn( $"Setting '{p.Path}' failed.", ex );
                message = $"Invalid value for '{p.Path}': {ex.Message}";
                return false;
            }
            var text = RunProperty.ToText( v );
            _records[p.Path] = new DynamicConfigurationRecord( text, _configuration[p.Path] );
            Save( monitor );
            monitor.Info( $"'{p.Path}' changed from '{RunProperty.ToText( actual )}' to '{text}'." );
            message = $"'{p.Path}' is '{text ?? "null"}'.";
            return true;
        }
    }

    void Save( IActivityMonitor monitor )
    {
        if( _store == null ) return;
        try
        {
            _store.Save( monitor, _records.ToDictionary( kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase ) );
        }
        catch( Exception ex )
        {
            monitor.Error( "While saving the dynamic configuration: the change is lost at the next start.", ex );
        }
    }
}
