using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.AmbientValues
{
    /// <summary>
    /// Defines an extensible set of properties that are global
    /// to a Client/Server context: the <see cref="IAmbientValuesCollectCommand"/> sent to
    /// the endpoint returns the values.
    /// </summary>
    public interface IAmbientValues : IPoco
    {
    }
}
