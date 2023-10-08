using CK.Setup;
using CK.Testing;
using CK.Testing.StObjEngine;
using Microsoft.Extensions.DependencyInjection;
using static CK.Testing.StObjEngineTestHelper;


namespace CK.Cris.Executor.Tests
{
    static class TestHelperExtensions
    {
        public static AutomaticServicesResult CreateAutomaticServicesWithMonitor( this IStObjEngineTestHelperCore h, StObjCollector c )
        {
            return h.CreateAutomaticServices( c,
                                              configureServices: r =>
                                              {
                                                  r.Services.AddScoped( sp => ((IMonitorTestHelper)h).Monitor );
                                                  r.Services.AddScoped( sp => ((IMonitorTestHelper)h).Monitor.ParallelLogger );
                                              } );
        }
    }
}
