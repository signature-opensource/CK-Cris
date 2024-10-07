using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;


namespace CK.Cris.Executor.Tests;

static class TestHelperExtensions
{
    // This reuses the TestHemper.Monitor.
    // This only works beacuse we don't use background execution in these tests!
    public static AutomaticServices CreateAutomaticServicesWithMonitor( this IMonitorTestHelper h, IEnumerable<Type> types )
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
