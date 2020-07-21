using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.AspNet
{
    [ExternalName( "CrisCommandResult" )]
    public interface IAspNetCommandResult : IPoco
    {
        VISAMCode Code { get; set; }

        object Result { get; set; }
    }
}
