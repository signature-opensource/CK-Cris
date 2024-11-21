using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace CK.Cris.Executor.Tests;

static class TestHelperExtensions
{
    // This reuses the TestHelper.Monitor.
    // This only works beacuse we don't use background execution in these tests!
    public static async Task<AutomaticServices> CreateAutomaticServicesWithMonitorAsync( this IMonitorTestHelper h, IEnumerable<Type> types )
    {
        var configuration = h.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( types );
        return (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices( configureServices: r =>
                                                {
                                                    r.AddScoped( sp => h.Monitor );
                                                    r.AddScoped( sp => h.Monitor.ParallelLogger );
                                                } );
    }
}
