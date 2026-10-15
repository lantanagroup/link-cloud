using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class AdvancedPerformanceApiRowsTests
{
    [Fact]
    public void Api_response_time_keeps_acquisition_api_and_drops_the_worker()
    {
        var doc = new AutomationRunMetricsDocument();
        doc.ApiLatency["acquisition"] = new ApiLatencySnapshot { Count = 10, P50Ms = 5, P95Ms = 9, P99Ms = 12 };
        doc.ApiLatency["acquisition-worker"] = new ApiLatencySnapshot { Unavailable = true };

        var rows = MetricsRunPresenter.ToApiLatency(doc);
        rows.Should().Contain(r => r.Key == "acquisition" && r.Name == "Data Acquisition");
        rows.Should().NotContain(r => r.Key == "acquisition-worker" || r.Name == "Data Acquisition worker");

        MetricsRunPresenter.EmptyUnavailableApiLatency()
            .Should().NotContain(r => r.Key == "acquisition-worker");
        RunMetricsSnapshotService.ApiLatencyQueries
            .Should().NotContain(q => q.ExportedJob == "DataAcquisitionWorker");
    }

    [Fact]
    public void Slowest_endpoints_never_list_the_worker()
    {
        var doc = new AutomationRunMetricsDocument();
        doc.SlowestApiRoutes =
        [
            new ApiRouteLatencySnapshot { Service = "Data Acquisition worker", Method = "GET", Route = "/api/info", P95Ms = 900, Count = 3 },
            new ApiRouteLatencySnapshot { Service = "Data Acquisition", Method = "POST", Route = "/api/data/{facilityId}/QueryPlan", P95Ms = 220, Count = 4 }
        ];

        var routes = MetricsRunPresenter.ToSlowestApiRoutes(doc);
        routes.Should().ContainSingle().Which.Service.Should().Be("Data Acquisition");
        RunMetricsSnapshotService.HasApi("DataAcquisitionWorker").Should().BeFalse();
        RunMetricsSnapshotService.HasApi("DataAcquisition.AcquisitionWorker").Should().BeFalse();
        RunMetricsSnapshotService.HasApi("DataAcquisition").Should().BeTrue();
    }
}