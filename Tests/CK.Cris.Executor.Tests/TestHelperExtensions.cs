using CK.Setup;
using CK.Testing;
using CK.Testing.Monitoring;
using CK.Testing.StObjEngine;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;


namespace CK.Cris.Executor.Tests
{
    static class TestHelperExtensions
    {
        public static AutomaticServices CreateAutomaticServicesWithMonitor( this IMonitorTestHelper h, ISet<Type> types )
        {
            var configuration = h.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( types );
            return configuration.RunSuccessfully().CreateAutomaticServices( configureServices: r =>
                                                    {
                                                        r.AddScoped( sp => h.Monitor );
                                                        r.AddScoped( sp => h.Monitor.ParallelLogger );
                                                    } );
        }
    }
}
