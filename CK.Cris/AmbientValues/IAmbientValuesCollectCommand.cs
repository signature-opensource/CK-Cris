using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.AmbientValues
{
    /// <summary>
    /// Defines a basic command that returns the <see cref="IAmbientValues"/>.
    /// <para>
    /// These ambient values are by default empty, hence this command is empty: the resulting values depend on ambient properties
    /// extensions available at a receiver point, like authentication informations, IP address, public keys, etc.
    /// </para>
    /// <para>
    /// This standard command comes with its default but rather definitive command handler (<see cref="AmbientValuesService.GetValues(IAmbientValuesCollectCommand)"/>)
    /// that instantiates an empty IAmbientValues instance: then, any number of <see cref="CommandPostHandlerAttribute"/> can be used to populate the
    /// ambient value properties.
    /// </para>
    /// </summary>
    public interface IAmbientValuesCollectCommand : ICommand<IAmbientValues>
    {
    }

}
