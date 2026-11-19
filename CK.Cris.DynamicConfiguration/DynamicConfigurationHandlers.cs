using CK.Auth;
using CK.Core;
using CK.IO.DynamicConfiguration;
using System.Collections.Generic;
using System.Linq;

namespace CK.Cris.DynamicConfiguration;

/// <summary>
/// Handles the commands of the dynamic configuration (CK.IO.DynamicConfiguration): they are party commands, executed by
/// the application of their target. No right is checked but the Critical level: any user that reaches it can change the
/// run properties, unless the application adds a validator. A change made while impersonating is refused: the operator
/// acts as itself.
/// </summary>
public sealed class DynamicConfigurationHandlers : ISingletonAutoService
{
    /// <summary>
    /// Refuses a change made while impersonating.
    /// </summary>
    /// <param name="c">The message collector.</param>
    /// <param name="command">The command.</param>
    /// <param name="authentication">The authentication.</param>
    [CommandHandlingValidator]
    public void Validate( UserMessageCollector c, IConfigurePropertyCommand command, AuthenticationInfo authentication )
    {
        if( authentication.IsImpersonated )
        {
            c.Error( "The configuration can't be changed while impersonating.", "DynamicConfiguration.Impersonated" );
        }
    }

    /// <summary>
    /// Changes a run property.
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="command">The command.</param>
    /// <param name="service">The dynamic configuration.</param>
    /// <param name="culture">The current culture (of the messages).</param>
    /// <returns>The result.</returns>
    [CommandHandler]
    public ICrisBasicCommandResult Handle( IActivityMonitor monitor, IConfigurePropertyCommand command, DynamicConfigurationService service, CurrentCultureInfo culture )
    {
        var success = service.Configure( monitor, command.PropertyPath, command.Value, command.CurrentValue, out var message );
        var result = command.CreateResult();
        result.Success = success;
        result.UserMessages.Add( success
                                    ? UserMessage.Info( culture, message, "DynamicConfiguration.Changed" )
                                    : UserMessage.Error( culture, message, "DynamicConfiguration.Refused" ) );
        return result;
    }

    /// <summary>
    /// Lists the run properties that can be changed.
    /// </summary>
    /// <param name="command">The query.</param>
    /// <param name="service">The dynamic configuration.</param>
    /// <param name="pocos">The poco directory.</param>
    /// <returns>The properties.</returns>
    [CommandHandler]
    public IList<IRunPropertyInfo> Handle( IGetRunPropertiesQCommand command, DynamicConfigurationService service, PocoDirectory pocos )
    {
        return service.Properties.Where( p => p.IsRun )
                                 .Select( p => pocos.Create<IRunPropertyInfo>( i =>
                                 {
                                     i.Path = p.Path;
                                     i.Type = p.ValueType.Name;
                                     if( p.ValueType.IsEnum ) foreach( var n in System.Enum.GetNames( p.ValueType ) ) i.EnumValues.Add( n );
                                     i.Value = RunProperty.ToText( service.GetValue( p ) );
                                 } ) )
                                 .ToList();
    }
}
