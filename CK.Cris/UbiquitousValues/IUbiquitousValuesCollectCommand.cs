using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.UbiquitousValues
{
    /// <summary>
    /// The command that returns the <see cref="IUbiquitousValues"/>.
    /// <para>
    /// This standard command comes with a default but rather definitive command handler (<see cref="UbiquitousValuesService.GetValues(IUbiquitousValuesCollectCommand)"/>)
    /// that instantiates an empty IUbiquitousValues instance: then, any number of <see cref="CommandPostHandlerAttribute"/> can be used to set
    /// the values.
    /// </para>
    /// </summary>
    public interface IUbiquitousValuesCollectCommand : ICommand<IUbiquitousValues>
    {
    }

}
