using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.AmbientValues
{
    /// <summary>
    /// Defines a basic command that returns a string to object dictionary of
    /// "ambient values".
    /// <para>
    /// These ambient values are, by design, not explicitly contextualized (hence this command is empty):
    /// the resulting values depend on ambient properties available at a receiver point, like authentication
    /// informations, IP address, public keys, etc.
    /// </para>
    /// <para>
    /// This standard command comes with its default but rather definitive command handler that instantiates
    /// an empty dictionary (<see cref="AmbientValuesCollectHandler"/>): any number of
    /// <see cref="CommandPostHandlerAttribute"/> can be used to populate the dictionary.
    /// </para>
    /// </summary>
    public interface IAmbientValuesCollectCommand : ICommand<Dictionary<string, object?>>
    {
    }

}
