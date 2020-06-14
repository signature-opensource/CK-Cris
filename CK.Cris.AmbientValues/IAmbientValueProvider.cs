using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;

namespace CK.Cris
{
    [IsMultiple]
    public interface IAmbientValueProvider : IAutoService
    {
        ValueTask CollectAmbientValues( IActivityMonitor monitor, IDictionary<string, object> ambientValues );
    }
}
