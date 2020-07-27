using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.AspNet
{
    [ExternalName( "CrisCommandResult" )]
    public interface IAspNetCommandResult : IPoco
    {
        VESACode Code { get; set; }

        object Result { get; set; }
    }
}
