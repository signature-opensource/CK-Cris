using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris
{
    public static class ActivityMonitorExtension
    {
        public static readonly CKTrait CommandCallerTarget = ActivityMonitor.Tags.Register( "cris:CommandCallerTarget" );

    }
}
