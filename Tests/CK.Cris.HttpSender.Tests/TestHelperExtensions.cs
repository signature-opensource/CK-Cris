using CK.Core;
using CK;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Cris.HttpSender.Tests
{
    // Temporary (not composable enough).
    public static class StObjMapExtensions
    {

        public static Task<ApplicationIdentityTestHelperExtension.RunningAppIdentity> CreateRunningCallerAsync( this IStObjMap map,
                                                                                                                string serverAddress,
                                                                                                                Action<MutableConfigurationSection>? configuration = null,
                                                                                                                bool generateSourceCode = true )
        {
            return TestHelper.CreateRunningAppIdentityServiceAsync(
                c =>
                {
                    c["FullName"] = "Domain/$Caller";
                    c["Parties:0:FullName"] = "Domain/$Server";
                    c["Parties:0:Address"] = serverAddress;
                    if( Debugger.IsAttached )
                    {
                        // One hour timeout when Debugger.IsAttached.
                        c["Parties:0:CrisHttpSender:Timeout"] = "00:01:00";
                    }
                    else
                    {
                        // Otherwise use the default 100 seconds timeout.
                        c["Parties:0:CrisHttpSender"] = "true";
                    }
                    configuration?.Invoke( c );
                },
                services =>
                {
                    services.AddStObjMap( TestHelper.Monitor, map );
                } );
        }
    }
}
