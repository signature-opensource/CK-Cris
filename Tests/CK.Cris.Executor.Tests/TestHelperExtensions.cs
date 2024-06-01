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
            return h.CreateSingleBinPathAutomaticServices( types,
                                                           configureServices: r =>
                                                           {
                                                              r.Services.AddScoped( sp => h.Monitor );
                                                              r.Services.AddScoped( sp => h.Monitor.ParallelLogger );
                                                           } );
        }
    }
}
