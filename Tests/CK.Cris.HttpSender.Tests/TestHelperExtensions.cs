using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Cris.HttpSender.Tests
{
    public static class TestHelperExtensions
    {

        public static Task<ApplicationIdentityTestHelperExtension.RunningAppIdentity> CreateRunningCallerAsync( this IMonitorTestHelper helper,
                                                                                                                string serverAddress,
                                                                                                                ISet<Type> registerTypes,
                                                                                                                Action<MutableConfigurationSection>? configuration = null,
                                                                                                                bool generateSourceCode = true )
        {
            var c = helper.CreateDefaultEngineConfiguration( generateSourceCode );
            var callerMap = helper.RunSingleBinPathAndLoad( c, registerTypes ).Map;

            return helper.CreateRunningAppIdentityServiceAsync(
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
                        // Otherwise use the default 1 minute timeout.
                        c["Parties:0:CrisHttpSender"] = "true";
                    }
                    configuration?.Invoke( c );
                },
                services =>
                {
                    services.AddStObjMap( helper.Monitor, callerMap );
                } );
        }
    }
}
