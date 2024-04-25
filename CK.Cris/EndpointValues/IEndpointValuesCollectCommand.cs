using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.EndpointValues
{
    /// <summary>
    /// Defines a basic command that returns the <see cref="IEndpointValues"/>.
    /// <para>
    /// This command is empty: the resulting values depend on 
    /// extensions available at a receiver point, like authentication informations, IP address, public keys, etc.
    /// </para>
    /// <para>
    /// This standard command comes with its default but rather definitive command handler (<see cref="EndpointValuesService.GetValues(IEndpointValuesCollectCommand)"/>)
    /// that instantiates an empty IEndpointValues instance: then, any number of <see cref="CommandPostHandlerAttribute"/> can be used to populate the
    /// endpoint value properties.
    /// </para>
    /// </summary>
    public interface IEndpointValuesCollectCommand : ICommand<IEndpointValues>
    {
    }

}
